using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingOpeningNotificationRepository : ISponsoredListingOpeningNotificationRepository
    {
        private readonly IApplicationDbContext context;

        public SponsoredListingOpeningNotificationRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<bool> ExistsAsync(string email, SponsorshipType sponsorshipType, int? subCategoryId)
        {
            return await this.context.SponsoredListingOpeningNotifications
                .AnyAsync(n => n.Email == email &&
                               n.SponsorshipType == sponsorshipType &&
                               n.SubCategoryId == subCategoryId &&
                               !n.IsReminderSent);
        }

        public async Task CreateAsync(SponsoredListingOpeningNotification notification)
        {
            await this.context.SponsoredListingOpeningNotifications.AddAsync(notification);
            await this.context.SaveChangesAsync();
        }

        public async Task<IEnumerable<SponsoredListingOpeningNotification>> GetPendingNotificationsAsync()
        {
            return await this.context.SponsoredListingOpeningNotifications
                .Where(n => !n.IsReminderSent)
                .OrderBy(n => n.SubscribedDate)
                .ToListAsync();
        }

        public async Task<bool> UpdateAsync(SponsoredListingOpeningNotification notification)
        {
            this.context.SponsoredListingOpeningNotifications.Update(notification);
            return await this.context.SaveChangesAsync() > 0;
        }

        public async Task MarkReminderAsSentAsync(int notificationId)
        {
            var notification = await this.context.SponsoredListingOpeningNotifications
                .FirstOrDefaultAsync(n => n.SponsoredListingOpeningNotificationId == notificationId);

            if (notification != null)
            {
                notification.IsReminderSent = true;
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<SponsoredListingOpeningNotification?> GetByIdAsync(int id)
        {
            return await this.context.SponsoredListingOpeningNotifications
                .FirstOrDefaultAsync(n => n.SponsoredListingOpeningNotificationId == id);
        }

        public async Task<IEnumerable<SponsoredListingOpeningNotification>> GetAllAsync()
        {
            return await this.context.SponsoredListingOpeningNotifications.ToListAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var notification = await this.context.SponsoredListingOpeningNotifications
                .FirstOrDefaultAsync(n => n.SponsoredListingOpeningNotificationId == id);

            if (notification == null)
            {
                return false;
            }

            this.context.SponsoredListingOpeningNotifications.Remove(notification);
            return await this.context.SaveChangesAsync() > 0;
        }

    }
}