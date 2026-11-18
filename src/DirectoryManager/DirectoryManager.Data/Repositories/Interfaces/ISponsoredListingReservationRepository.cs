using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingReservationRepository
    {
        Task<SponsoredListingReservation> CreateReservationAsync(
            DateTime expirationDateTime,
            string reservationGroup,
            string details);

        /// <summary>
        /// Creates a hold only if the group still has room: fewer than <paramref name="maxActiveReservations"/>
        /// live holds in <paramref name="reservationGroup"/> and, when <paramref name="exclusiveGroup"/> is given,
        /// no live hold at all in that group (the main-sponsor per-subcategory "shadow" group). When it succeeds
        /// and an exclusive group is given, a shadow hold with the same expiration is written in that group too.
        /// The count and the insert run under a lock (process-wide, plus a Postgres advisory transaction lock),
        /// so two concurrent buyers can never both take the last slot.
        /// Returns null when there is no room.
        /// </summary>
        /// <returns>The new reservation, or null when the slot is full or held.</returns>
        Task<SponsoredListingReservation?> TryCreateReservationWithinCapacityAsync(
            DateTime expirationDateTime,
            string reservationGroup,
            string details,
            int maxActiveReservations,
            string? exclusiveGroup = null,
            string? exclusiveGroupDetails = null);

        Task<SponsoredListingReservation?> GetReservationByGuidAsync(Guid reservationId);
        Task<int> GetActiveReservationsCountAsync(string reservationGroup);

        /// <summary>
        /// Live holds in the group that belong to a listing OTHER than <paramref name="directoryEntryId"/>
        /// (matched on the "ListingId={id};" marker in the details).
        /// </summary>
        /// <returns>The count.</returns>
        Task<int> GetActiveReservationsCountExcludingEntryAsync(string reservationGroup, int directoryEntryId);

        /// <summary>
        /// The GUID of an active reservation currently held for the given directory entry within the
        /// given slot group, or null. Matched on the entry (via the reservation details) so a buyer's own
        /// hold can be recovered from the URL params (sponsor type + entry id) without a cookie, and no
        /// one can piggyback on someone else's reservation.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<Guid?> GetActiveReservationGuidForEntryAsync(int directoryEntryId, string reservationGroup);
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