using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsorInterestLogRepository : ISponsorInterestLogRepository
    {
        private readonly IApplicationDbContext context;

        public SponsorInterestLogRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task LogAsync(int directoryEntryId, SponsorshipType type, int? typeId, SponsorInterestStep step, TimeSpan dedupeWindow)
        {
            var now = DateTime.UtcNow;
            var since = now - dedupeWindow;
            bool recent = await this.context.SponsorInterestLogs.AnyAsync(x =>
                x.DirectoryEntryId == directoryEntryId
                && x.SponsorshipType == type
                && x.TypeId == typeId
                && x.Step == step
                && x.CreateDate >= since);
            if (recent)
            {
                return;
            }

            await this.context.SponsorInterestLogs.AddAsync(new SponsorInterestLog
            {
                DirectoryEntryId = directoryEntryId,
                SponsorshipType = type,
                TypeId = typeId,
                Step = step,
                CreateDate = now,
            });
            await this.context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<SponsorInterestLog>> GetInRangeAsync(DateTime start, DateTime end)
        {
            return await this.context.SponsorInterestLogs
                .Where(x => x.CreateDate >= start && x.CreateDate < end)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Dictionary<int, DateTime>> GetPaidEntryIdsAsync(DateTime start, DateTime end)
        {
            return await this.context.SponsoredListingInvoices
                .Where(i => i.PaymentStatus == PaymentStatus.Paid && i.CreateDate >= start && i.CreateDate < end)
                .GroupBy(i => i.DirectoryEntryId)
                .Select(g => new { g.Key, Last = g.Max(i => i.CreateDate) })
                .ToDictionaryAsync(x => x.Key, x => x.Last);
        }
    }
}
