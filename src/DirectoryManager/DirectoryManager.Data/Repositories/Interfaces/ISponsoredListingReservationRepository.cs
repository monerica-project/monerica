using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingReservationRepository
    {
        Task<SponsoredListingReservation> CreateReservationAsync(DateTime expirationDateTime, string reservationGroup);
        Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId);
        Task<int> GetActiveReservationsCountAsync(string reservationGroup);
        Task<DateTime?> GetActiveReservationExpirationAsync(string reservationGroup);

        /// <summary>
        /// Returns an active reservation GUID for the given group if one exists,
        /// or null if none exists. Prefer the most recently created / latest expiring one.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<Guid?> GetAnyActiveReservationGuidAsync(string reservationGroup);
    }
}