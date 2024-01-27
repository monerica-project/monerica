using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingReservationRepository
    {
        Task<SponsoredListingReservation> CreateOrUpdateReservationAsync(DateTime expirationDateTime);
        Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId);
    }
}