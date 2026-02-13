using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
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

        public async Task<bool> ExistsAsync(string email, SponsorshipType sponsorshipType, int? typeId)
        {
            var e = NormalizeEmail(email);
            var scopeTypeId = NormalizeTypeId(sponsorshipType, typeId);

            var q = this.context.SponsoredListingOpeningNotifications
                .AsNoTracking()
                .Where(n =>
                    n.Email == e &&
                    n.SponsorshipType == sponsorshipType &&
                    n.IsActive &&
                    !n.IsReminderSent);

            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                q = q.Where(n => n.TypeId == null || n.TypeId == 0);
            }
            else
            {
                if (!scopeTypeId.HasValue)
                {
                    return false;
                }

                q = q.Where(n => n.TypeId == scopeTypeId.Value);
            }

            return await q.AnyAsync().ConfigureAwait(false);
        }

        public async Task CreateAsync(SponsoredListingOpeningNotification notification)
        {
            try
            {
                await this.context.SponsoredListingOpeningNotifications.AddAsync(notification);
                await this.context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException ?? ex);
            }
        }

        public async Task<IEnumerable<SponsoredListingOpeningNotification>> GetSubscribers()
        {
            // Oldest-first is better for processing reminders (fair queue),
            // but must include IsActive + !IsReminderSent.
            return await this.context.SponsoredListingOpeningNotifications
                .Where(n => n.IsActive && !n.IsReminderSent)
                .OrderBy(n => n.SubscribedDate)
                .ThenBy(n => n.SponsoredListingOpeningNotificationId)
                .ToListAsync();
        }

        public async Task<bool> UpdateAsync(SponsoredListingOpeningNotification notification)
        {
            this.context.SponsoredListingOpeningNotifications.Update(notification);
            return await this.context.SaveChangesAsync() > 0;
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

        // ----------------------------
        // UPSERTS (WORKING + FAST)
        // ----------------------------

        // Existing signature used in older flow
        public Task UpsertAsync(int directoryEntryId, SponsorshipType type, string email, int? typeId)
        {
            return this.UpsertManyAsync(email, directoryEntryId, new[] { (type, typeId) });
        }

        // New signature used by your new flow/controller
        public Task UpsertAsync(string email, SponsorshipType sponsorshipType, int? typeId, int? directoryEntryId)
        {
            if (!directoryEntryId.HasValue || directoryEntryId.Value <= 0)
            {
                return Task.CompletedTask;
            }

            return this.UpsertManyAsync(email, directoryEntryId.Value, new[] { (sponsorshipType, typeId) });
        }

        public Task<int> GetWaitlistCountAsync(SponsorshipType type, int? typeId)
        {
            var scopeTypeId = NormalizeTypeId(type, typeId);
            var q = this.BaseWaitlistQuery(type, scopeTypeId);
            return q.CountAsync();
        }

        public async Task<List<WaitlistItemDto>> GetWaitlistPreviewAsync(SponsorshipType type, int? typeId, int take)
        {
            if (take < 1)
            {
                take = 1;
            }

            var scopeTypeId = NormalizeTypeId(type, typeId);

            var q = this.BaseWaitlistQuery(type, scopeTypeId)
                .OrderByDescending(n => n.SubscribedDate)
                .ThenByDescending(n => n.SponsoredListingOpeningNotificationId)
                .Select(n => new WaitlistItemDto
                {
                    SponsoredListingOpeningNotificationId = n.SponsoredListingOpeningNotificationId,
                    Email = n.Email,
                    SponsorshipType = n.SponsorshipType,
                    TypeId = n.TypeId,
                    DirectoryEntryId = n.DirectoryEntryId,
                    CreateDateUtc = n.SubscribedDate
                });

            return await q.Take(take).ToListAsync().ConfigureAwait(false);
        }

        public async Task<(List<WaitlistItemDto> Items, int TotalCount)> GetWaitlistPagedAsync(
            SponsorshipType type,
            int? typeId,
            int page,
            int pageSize)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            var scopeTypeId = NormalizeTypeId(type, typeId);

            var baseQ = this.BaseWaitlistQuery(type, scopeTypeId);

            var total = await baseQ.CountAsync().ConfigureAwait(false);

            var q = baseQ
                .OrderByDescending(n => n.SubscribedDate)
                .ThenByDescending(n => n.SponsoredListingOpeningNotificationId)
                .Select(n => new WaitlistItemDto
                {
                    SponsoredListingOpeningNotificationId = n.SponsoredListingOpeningNotificationId,
                    Email = n.Email,
                    SponsorshipType = n.SponsorshipType,
                    TypeId = n.TypeId,
                    DirectoryEntryId = n.DirectoryEntryId,
                    CreateDateUtc = n.SubscribedDate
                });

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);

            return (items, total);
        }

        public Task MarkReminderAsSentAsync(int notificationId)
        {
            return this.MarkReminderAsSentAsync(notificationId, sentLink: null);
        }

        public async Task MarkReminderAsSentAsync(int notificationId, string? sentLink)
        {
            var notification = await this.context.SponsoredListingOpeningNotifications
                .FirstOrDefaultAsync(n => n.SponsoredListingOpeningNotificationId == notificationId);

            if (notification == null)
            {
                return;
            }

            notification.IsReminderSent = true;
            notification.IsActive = false;

            // audit fields
            notification.ReminderSentDateUtc = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(sentLink))
            {
                notification.ReminderSentLink = sentLink.Trim();
            }

            notification.UpdateDate = DateTime.UtcNow;

            await this.context.SaveChangesAsync();
        }

        public async Task UpsertManyAsync(
            string email,
            int directoryEntryId,
            IEnumerable<(SponsorshipType Type, int? TypeId)> scopes)
        {
            var e = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(e))
            {
                return;
            }

            if (directoryEntryId <= 0)
            {
                return;
            }

            var list = (scopes ?? Enumerable.Empty<(SponsorshipType, int?)>())
                .Distinct()
                .ToList();

            if (list.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;

            foreach (var (type, rawTypeId) in list)
            {
                await this.UpsertCoreNoSaveAsync(
                    emailNormalized: e,
                    sponsorshipType: type,
                    rawTypeId: rawTypeId,
                    directoryEntryId: directoryEntryId,
                    nowUtc: now).ConfigureAwait(false);
            }

            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        // MainSponsor: store TypeId = null (but we will also match legacy TypeId == 0)
        private static int? NormalizeTypeId(SponsorshipType type, int? typeId)
        {
            if (type == SponsorshipType.MainSponsor)
            {
                return null;
            }

            if (!typeId.HasValue || typeId.Value <= 0)
            {
                return null;
            }

            return typeId.Value;
        }

        private IQueryable<SponsoredListingOpeningNotification> BaseWaitlistQuery(
            SponsorshipType type,
            int? typeIdNormalized)
        {
            var q = this.context.SponsoredListingOpeningNotifications
                .AsNoTracking()
                .Where(n =>
                    n.IsActive &&
                    !n.IsReminderSent &&
                    n.SponsorshipType == type);

            if (type == SponsorshipType.MainSponsor)
            {
                // Accept both null and legacy 0 to avoid “losing” older rows
                q = q.Where(n => n.TypeId == null || n.TypeId == 0);
            }
            else
            {
                // If scope invalid, return empty
                if (!typeIdNormalized.HasValue)
                {
                    return q.Where(_ => false);
                }

                q = q.Where(n => n.TypeId == typeIdNormalized.Value);
            }

            return q;
        }

        private async Task UpsertCoreNoSaveAsync(
            string emailNormalized,
            SponsorshipType sponsorshipType,
            int? rawTypeId,
            int directoryEntryId,
            DateTime nowUtc)
        {
            var scopeTypeId = NormalizeTypeId(sponsorshipType, rawTypeId);

            // If not main sponsor and scope is missing, do nothing.
            if (sponsorshipType != SponsorshipType.MainSponsor && !scopeTypeId.HasValue)
            {
                return;
            }

            // Match existing regardless of IsReminderSent so re-subscribe revives it.
            // IMPORTANT: Use non-nullable directoryEntryId to avoid the "IS NULL" trap.
            var query = this.context.SponsoredListingOpeningNotifications
                .Where(n =>
                    n.Email == emailNormalized &&
                    n.SponsorshipType == sponsorshipType &&
                    (
                        n.DirectoryEntryId == directoryEntryId
                        || n.DirectoryEntryId == null)); // legacy fallback

            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                query = query.Where(n => n.TypeId == null || n.TypeId == 0);
            }
            else
            {
                query = query.Where(n => n.TypeId == scopeTypeId!.Value);
            }

            var existing = await query
                .OrderByDescending(n => n.SubscribedDate)
                .ThenByDescending(n => n.SponsoredListingOpeningNotificationId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing == null)
            {
                this.context.SponsoredListingOpeningNotifications.Add(new SponsoredListingOpeningNotification
                {
                    DirectoryEntryId = directoryEntryId,     // REQUIRED
                    SponsorshipType = sponsorshipType,
                    TypeId = scopeTypeId,                    // main => null
                    Email = emailNormalized,
                    CreateDate = nowUtc,
                    SubscribedDate = nowUtc,
                    UpdateDate = nowUtc,

                    IsActive = true,
                    IsReminderSent = false,
                    ReminderSentDateUtc = null,
                    ReminderSentLink = null
                });

                return;
            }
            else

            existing.DirectoryEntryId = directoryEntryId;
            existing.TypeId = scopeTypeId;
            existing.SubscribedDate = nowUtc;
            existing.UpdateDate = nowUtc;

            existing.IsActive = true;
            existing.IsReminderSent = false;
            existing.ReminderSentDateUtc = null;
            existing.ReminderSentLink = null;

            // Clean up legacy main sponsor rows stored with TypeId=0
            if (sponsorshipType == SponsorshipType.MainSponsor && existing.TypeId == 0)
            {
                existing.TypeId = null;
            }
        }
    }
}