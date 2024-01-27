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

        public async Task<SponsoredListingReservation> CreateOrUpdateReservationAsync(DateTime expirationDateTime)
        {
            var reservationId = Guid.NewGuid();
            var reservation = await this.context.SponsoredListingReservations
                .FirstOrDefaultAsync(
                    r => r.ReservationId == reservationId && r.ExpirationDateTime >
                        DateTime.UtcNow.AddMinutes(-IntegerConstants.SponsoredReservationExpirationMinutes));

            if (reservation == null)
            {
                reservation = new SponsoredListingReservation
                {
                    ReservationId = reservationId,
                    ExpirationDateTime = expirationDateTime
                };
                this.context.SponsoredListingReservations.Add(reservation);
            }
            else
            {
                reservation.ExpirationDateTime = expirationDateTime;
            }

            await this.context.SaveChangesAsync();
            return reservation;
        }

        public async Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId)
        {
            SponsoredListingReservation? sponsoredListingReservation = await this.context.SponsoredListingReservations
                                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            return sponsoredListingReservation;
        }
    }
}