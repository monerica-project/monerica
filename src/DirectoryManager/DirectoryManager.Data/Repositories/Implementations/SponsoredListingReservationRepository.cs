using DirectoryManager.Data.Constants;
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

        public async Task<SponsoredListingReservation> CreateReservationAsync(DateTime expirationDateTime)
        {
            var reservationId = Guid.NewGuid();
            var reservation = new SponsoredListingReservation
            {
                ReservationGuid = reservationId,
                ExpirationDateTime = expirationDateTime
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

        public async Task<int> GetActiveReservationsCountAsync()
        {
            var currentDate = DateTime.UtcNow;
            return await this.context.SponsoredListingReservations
                .CountAsync(r => r.ExpirationDateTime > currentDate);
        }
    }
}