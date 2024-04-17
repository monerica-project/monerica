using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingReservationRepository
    {
        Task<SponsoredListingReservation> CreateReservationAsync(DateTime expirationDateTime, string reservationGroup);
        Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId);
        Task<int> GetActiveReservationsCountAsync(string reservationGroup);
    }
}