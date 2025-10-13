using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingReservationRepository : ISponsoredListingReservationRepository
    {
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
    }
}