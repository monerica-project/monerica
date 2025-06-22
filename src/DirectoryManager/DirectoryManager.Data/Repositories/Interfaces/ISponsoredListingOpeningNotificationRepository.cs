using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingOpeningNotificationRepository
    {
        /// <summary>
        /// Checks if a notification already exists for the given email, sponsorship type, and optional subcategory.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> ExistsAsync(string email, SponsorshipType sponsorshipType, int? subCategoryId);

        /// <summary>
        /// Creates a new Sponsored Listing Opening Notification.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task CreateAsync(SponsoredListingOpeningNotification notification);

        /// <summary>
        /// Retrieves all notifications that are pending reminders.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<IEnumerable<SponsoredListingOpeningNotification>> GetSubscribers();

        /// <summary>
        /// Marks a notification as having its reminder sent.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task MarkReminderAsSentAsync(int notificationId);

        Task<bool> UpdateAsync(SponsoredListingOpeningNotification notification);

        Task<SponsoredListingOpeningNotification?> GetByIdAsync(int id);

        Task<IEnumerable<SponsoredListingOpeningNotification>> GetAllAsync();

        Task<bool> DeleteAsync(int id);
    }
}