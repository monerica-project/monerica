 
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class AffiliateCommissionEarnedRepository : IAffiliateCommissionEarnedRepository
    {
        private readonly IApplicationDbContext context;

        public AffiliateCommissionEarnedRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<AffiliateCommissionEarned?> GetByIdAsync(int affiliateCommissionId)
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Include(c => c.DirectoryEntry)
                .FirstOrDefaultAsync(c => c.AffiliateCommissionEarnedId == affiliateCommissionId);
        }

        public async Task<IEnumerable<AffiliateCommissionEarned>> GetAllAsync()
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Include(c => c.DirectoryEntry)
                .OrderByDescending(c => c.CommissionDate)
                .ToListAsync();
        }

        public async Task<(IEnumerable<AffiliateCommissionEarned> Items, int TotalCount)> GetPagedAsync(
            int page,
            int pageSize)
        {
            var query = this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Include(c => c.DirectoryEntry)
                .OrderByDescending(c => c.CommissionDate);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<(IEnumerable<AffiliateCommissionEarned> Items, int TotalCount)> GetPagedByDirectoryEntryAsync(
            int directoryEntryId,
            int page,
            int pageSize)
        {
            var query = this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Include(c => c.DirectoryEntry)
                .Where(c => c.DirectoryEntryId == directoryEntryId)
                .OrderByDescending(c => c.CommissionDate);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<(IEnumerable<AffiliateCommissionEarned> Items, int TotalCount)> GetPagedByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            int page,
            int pageSize)
        {
            var query = this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Include(c => c.DirectoryEntry)
                .Where(c => c.CommissionDate >= startDate && c.CommissionDate <= endDate)
                .OrderByDescending(c => c.CommissionDate);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<decimal> GetTotalUsdValueAsync()
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .SumAsync(c => (decimal?)c.UsdValue) ?? 0m;
        }

        public async Task<decimal> GetTotalUsdValueByDirectoryEntryAsync(int directoryEntryId)
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Where(c => c.DirectoryEntryId == directoryEntryId)
                .SumAsync(c => (decimal?)c.UsdValue) ?? 0m;
        }

        public async Task<decimal> GetTotalUsdValueByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Where(c => c.CommissionDate >= startDate && c.CommissionDate <= endDate)
                .SumAsync(c => (decimal?)c.UsdValue) ?? 0m;
        }

        public async Task<IEnumerable<AffiliateCommissionEarnedTotal>> GetTotalsByDirectoryEntryAsync()
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .GroupBy(c => new { c.DirectoryEntryId, c.DirectoryEntry!.Name })
                .Select(g => new AffiliateCommissionEarnedTotal
                {
                    DirectoryEntryId = g.Key.DirectoryEntryId,
                    DirectoryEntryName = g.Key.Name,
                    TotalUsdValue = g.Sum(x => x.UsdValue),
                    CommissionCount = g.Count(),
                    FirstCommissionDate = g.Min(x => (DateTime?)x.CommissionDate),
                    LastCommissionDate = g.Max(x => (DateTime?)x.CommissionDate)
                })
                .OrderByDescending(t => t.TotalUsdValue)
                .ToListAsync();
        }

        public async Task<IEnumerable<AffiliateCommissionEarnedTotal>> GetTotalsByDirectoryEntryAsync(
            DateTime startDate,
            DateTime endDate)
        {
            return await this.context.AffiliateCommissionsEarned
                .AsNoTracking()
                .Where(c => c.CommissionDate >= startDate && c.CommissionDate <= endDate)
                .GroupBy(c => new { c.DirectoryEntryId, c.DirectoryEntry!.Name })
                .Select(g => new AffiliateCommissionEarnedTotal
                {
                    DirectoryEntryId = g.Key.DirectoryEntryId,
                    DirectoryEntryName = g.Key.Name,
                    TotalUsdValue = g.Sum(x => x.UsdValue),
                    CommissionCount = g.Count(),
                    FirstCommissionDate = g.Min(x => (DateTime?)x.CommissionDate),
                    LastCommissionDate = g.Max(x => (DateTime?)x.CommissionDate)
                })
                .OrderByDescending(t => t.TotalUsdValue)
                .ToListAsync();
        }

        public async Task<AffiliateCommissionEarned> CreateAsync(AffiliateCommissionEarned commission)
        {
            commission.CreateDate = DateTime.UtcNow;

            this.context.AffiliateCommissionsEarned.Add(commission);
            await this.context.SaveChangesAsync();

            return commission;
        }

        public async Task UpdateAsync(AffiliateCommissionEarned commission)
        {
            var existing = await this.context.AffiliateCommissionsEarned
                .FirstOrDefaultAsync(c => c.AffiliateCommissionEarnedId == commission.AffiliateCommissionEarnedId)
                ?? throw new InvalidOperationException(
                    $"AffiliateCommissionEarned with id {commission.AffiliateCommissionEarnedId} not found.");

            existing.DirectoryEntryId = commission.DirectoryEntryId;
            existing.CommissionDate = commission.CommissionDate;
            existing.UsdValue = commission.UsdValue;
            existing.PaymentCurrency = commission.PaymentCurrency;
            existing.PaymentCurrencyAmount = commission.PaymentCurrencyAmount;
            existing.TransactionId = commission.TransactionId;
            existing.Note = commission.Note;
            existing.UpdatedByUserId = commission.UpdatedByUserId;
            existing.UpdateDate = DateTime.UtcNow;

            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int affiliateCommissionId)
        {
            var entity = await this.context.AffiliateCommissions
                .FirstOrDefaultAsync(c => c.AffiliateCommissionId == affiliateCommissionId);

            if (entity == null)
            {
                return;
            }

            this.context.AffiliateCommissions.Remove(entity);
            await this.context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int affiliateCommissionId)
        {
            return await this.context.AffiliateCommissions
                .AsNoTracking()
                .AnyAsync(c => c.AffiliateCommissionId == affiliateCommissionId);
        }
    }
}