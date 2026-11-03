using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingOpeningNotificationRepository
    {
        Task<bool> ExistsAsync(string email, SponsorshipType sponsorshipType, int? typeId);

        Task CreateAsync(SponsoredListingOpeningNotification notification);

        Task<IEnumerable<SponsoredListingOpeningNotification>> GetSubscribers();

        Task<bool> UpdateAsync(SponsoredListingOpeningNotification notification);

        Task MarkReminderAsSentAsync(int notificationId);

        Task<SponsoredListingOpeningNotification?> GetByIdAsync(int id);

        Task<IEnumerable<SponsoredListingOpeningNotification>> GetAllAsync();

        Task<bool> DeleteAsync(int id);

        // --- Waitlist / FOMO ---
        Task UpsertAsync(string email, SponsorshipType sponsorshipType, int? typeId, int? directoryEntryId);

        Task<int> GetWaitlistCountAsync(SponsorshipType type, int? typeId);

        /// <summary>
        /// Newest time any waitlist signup was added or changed (joined, left, or notified), or null
        /// when there are none. The sitemap uses it so a waitlist change refreshes the static site.
        /// </summary>
        Task<DateTime?> GetLastWaitlistChangeUtcAsync();

        Task<List<WaitlistItemDto>> GetWaitlistPreviewAsync(SponsorshipType type, int? typeId, int take);

        Task<(List<WaitlistItemDto> Items, int TotalCount)> GetWaitlistPagedAsync(
            SponsorshipType type,
            int? typeId,
            int page,
            int pageSize);

        Task MarkReminderAsSentAsync(int notificationId, string? sentLink);
        Task UpsertManyAsync(
            string email,
            int directoryEntryId,
            IEnumerable<(SponsorshipType Type, int? TypeId)> scopes);

        Task<List<WaitlistScopedItemDto>> GetWaitlistAllByTypeAsync(SponsorshipType type);
    }
}
