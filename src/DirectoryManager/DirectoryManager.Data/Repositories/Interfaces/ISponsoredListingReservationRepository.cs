using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingReservationRepository
    {
        Task<SponsoredListingReservation> CreateReservationAsync(
            DateTime expirationDateTime,
            string reservationGroup,
            string details);

        Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId);
        Task<int> GetActiveReservationsCountAsync(string reservationGroup);
        Task<DateTime?> GetActiveReservationExpirationAsync(string reservationGroup);

        /// <summary>
        /// Returns an active reservation GUID for the given group if one exists,
        /// or null if none exists. Prefer the most recently created / latest expiring one.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<Guid?> GetAnyActiveReservationGuidAsync(string reservationGroup);

        /// <summary>
        /// Extends expiration if the new value is later than the current value.
        /// Returns false if the reservation doesn't exist.
        /// </summary>
        /// <returns>It if extended.</returns>
        Task<bool> ExtendExpirationAsync(Guid reservationGuid, DateTime newExpirationUtc);
    }
}