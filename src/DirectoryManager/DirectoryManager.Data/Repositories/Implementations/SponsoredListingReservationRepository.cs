using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingReservationRepository : ISponsoredListingReservationRepository
    {
        // Arbitrary fixed key for the Postgres advisory lock that serializes slot-hold creation.
        private const long AdvisoryLockKey = 7311850217340001L;

        private static readonly SemaphoreSlim CreateLock = new (1, 1);

        private readonly IApplicationDbContext context;

        public SponsoredListingReservationRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<SponsoredListingReservation> CreateReservationAsync(
            DateTime expirationDateTime,
            string reservationGroup)
        {
            var reservationId = Guid.NewGuid();
            var reservation = new SponsoredListingReservation
            {
                ReservationGuid = reservationId,
                ExpirationDateTime = expirationDateTime,
                ReservationGroup = reservationGroup
            };

            this.context.SponsoredListingReservations.Add(reservation);
            await this.context.SaveChangesAsync();

            return reservation;
        }

        public async Task<SponsoredListingReservation> CreateReservationAsync(
            DateTime expirationDateTime,
            string reservationGroup,
            string details)
        {
            var reservationId = Guid.NewGuid();
            var reservation = new SponsoredListingReservation
            {
                ReservationGuid = reservationId,
                ExpirationDateTime = expirationDateTime,
                ReservationGroup = reservationGroup,
                Details = details
            };

            this.context.SponsoredListingReservations.Add(reservation);
            await this.context.SaveChangesAsync();

            return reservation;
        }

        public async Task<SponsoredListingReservation?> TryCreateReservationWithinCapacityAsync(
            DateTime expirationDateTime,
            string reservationGroup,
            string details,
            int maxActiveReservations,
            string? exclusiveGroup = null,
            string? exclusiveGroupDetails = null)
        {
            if (maxActiveReservations <= 0)
            {
                return null;
            }

            // Process-wide lock: the web app runs as one process, and this also covers providers
            // without advisory locks (the in-memory test database).
            await CreateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.context is DbContext db && db.Database.IsNpgsql())
                {
                    // Cross-process lock held for the transaction, so a second app instance (or a
                    // restart overlap) can't interleave its count + insert with ours.
                    var strategy = db.Database.CreateExecutionStrategy();
                    return await strategy.ExecuteAsync(async () =>
                    {
                        await using var tx = await db.Database.BeginTransactionAsync().ConfigureAwait(false);
                        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})").ConfigureAwait(false);
                        var created = await this.CountAndInsertAsync(expirationDateTime, reservationGroup, details, maxActiveReservations, exclusiveGroup, exclusiveGroupDetails).ConfigureAwait(false);
                        await tx.CommitAsync().ConfigureAwait(false);
                        return created;
                    }).ConfigureAwait(false);
                }

                return await this.CountAndInsertAsync(expirationDateTime, reservationGroup, details, maxActiveReservations, exclusiveGroup, exclusiveGroupDetails).ConfigureAwait(false);
            }
            finally
            {
                CreateLock.Release();
            }
        }

        public async Task<int> GetActiveReservationsCountExcludingEntryAsync(string reservationGroup, int directoryEntryId)
        {
            var now = DateTime.UtcNow;
            var marker = $"ListingId={directoryEntryId};";
            return await this.context.SponsoredListingReservations
                .Where(r => r.ReservationGroup == reservationGroup && r.ExpirationDateTime > now)
                .CountAsync(r => r.Details == null || !r.Details.Contains(marker));
        }

        public async Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId)
        {
            SponsoredListingReservation? sponsoredListingReservation = await this.context.SponsoredListingReservations
                                .FirstOrDefaultAsync(r => r.ReservationGuid == reservationId);

            return sponsoredListingReservation;
        }

        public async Task<int> GetActiveReservationsCountAsync(string reservationGroup)
        {
            var currentDate = DateTime.UtcNow;
            return await this.context
                             .SponsoredListingReservations
                             .Where(x => x.ReservationGroup == reservationGroup)
                             .CountAsync(r => r.ExpirationDateTime > currentDate);
        }

        public async Task<Guid?> GetActiveReservationGuidForEntryAsync(int directoryEntryId, string reservationGroup)
        {
            var currentDate = DateTime.UtcNow;

            // Reservation details are built as: Type={type}; ListingId={entryId}; ListingName="...".
            // The trailing ';' makes the marker exact (ListingId=30; won't match ListingId=300;).
            var marker = $"ListingId={directoryEntryId};";

            var reservation = await this.context
                                        .SponsoredListingReservations
                                        .Where(r => r.ReservationGroup == reservationGroup
                                            && r.ExpirationDateTime > currentDate
                                            && r.Details != null
                                            && r.Details.Contains(marker))
                                        .OrderByDescending(r => r.ExpirationDateTime)
                                        .FirstOrDefaultAsync();

            return reservation?.ReservationGuid;
        }

        public async Task<DateTime?> GetActiveReservationExpirationAsync(string reservationGroup)
        {
            var now = DateTime.UtcNow;

            // find the earliest expiration among still‑alive reservations
            var expiration = await this.context.SponsoredListingReservations
                .Where(r => r.ReservationGroup == reservationGroup && r.ExpirationDateTime > now)
                .OrderBy(r => r.ExpirationDateTime)
                .Select(r => (DateTime?)r.ExpirationDateTime)
                .FirstOrDefaultAsync();

            return expiration;  // null if none active
        }

        public async Task<Guid?> GetAnyActiveReservationGuidAsync(string reservationGroup)
        {
            var now = DateTime.UtcNow;
            return await this.context.SponsoredListingReservations
                .Where(r => r.ReservationGroup == reservationGroup && r.ExpirationDateTime > now)
                .OrderByDescending(r => r.ExpirationDateTime)
                .Select(r => (Guid?)r.ReservationGuid)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> ExtendExpirationAsync(Guid reservationGuid, DateTime newExpirationUtc)
        {
            var r = await this.context.SponsoredListingReservations
                .FirstOrDefaultAsync(x => x.ReservationGuid == reservationGuid);

            if (r == null)
            {
                return false;
            }

            if (newExpirationUtc > r.ExpirationDateTime)
            {
                r.ExpirationDateTime = newExpirationUtc;
                await this.context.SaveChangesAsync();
            }

            return true;
        }

        private async Task<SponsoredListingReservation?> CountAndInsertAsync(
            DateTime expirationDateTime,
            string reservationGroup,
            string details,
            int maxActiveReservations,
            string? exclusiveGroup,
            string? exclusiveGroupDetails)
        {
            if (await this.GetActiveReservationsCountAsync(reservationGroup).ConfigureAwait(false) >= maxActiveReservations)
            {
                return null;
            }

            if (exclusiveGroup != null
                && await this.GetActiveReservationsCountAsync(exclusiveGroup).ConfigureAwait(false) > 0)
            {
                return null;
            }

            var reservation = new SponsoredListingReservation
            {
                ReservationGuid = Guid.NewGuid(),
                ExpirationDateTime = expirationDateTime,
                ReservationGroup = reservationGroup,
                Details = details,
            };
            this.context.SponsoredListingReservations.Add(reservation);

            SponsoredListingReservation? shadow = null;
            if (exclusiveGroup != null)
            {
                shadow = new SponsoredListingReservation
                {
                    ReservationGuid = Guid.NewGuid(),
                    ExpirationDateTime = expirationDateTime,
                    ReservationGroup = exclusiveGroup,
                    Details = exclusiveGroupDetails ?? details,
                };
                this.context.SponsoredListingReservations.Add(shadow);
            }

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch
            {
                // Don't leave the unsaved rows tracked: an execution-strategy retry would add them twice.
                this.context.SponsoredListingReservations.Entry(reservation).State = EntityState.Detached;
                if (shadow != null)
                {
                    this.context.SponsoredListingReservations.Entry(shadow).State = EntityState.Detached;
                }

                throw;
            }

            return reservation;
        }
    }
}