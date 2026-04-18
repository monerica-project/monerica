using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class AffiliateCommissionRepository : IAffiliateCommissionRepository
    {
        private readonly IApplicationDbContext context;

        public AffiliateCommissionRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        private DbSet<AffiliateCommission> Set => this.context.AffiliateCommissions;

        public async Task<AffiliateCommission?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.AsNoTracking().FirstOrDefaultAsync(c => c.AffiliateCommissionId == id, ct);

        public async Task<AffiliateCommission?> GetByInvoiceIdAsync(int sponsoredListingInvoiceId, CancellationToken ct = default) =>
            await this.Set.AsNoTracking().FirstOrDefaultAsync(c => c.SponsoredListingInvoiceId == sponsoredListingInvoiceId, ct);

        public async Task<AffiliateCommission> AddAsync(AffiliateCommission entity, CancellationToken ct = default)
        {
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
            return entity;
        }

        public async Task UpdateAsync(AffiliateCommission entity, CancellationToken ct = default)
        {
            this.Set.Update(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task<bool> ExistsForInvoiceAsync(int sponsoredListingInvoiceId, CancellationToken ct = default)
        {
            return await this.context.AffiliateCommissions
                .AsNoTracking()
                .AnyAsync(x => x.SponsoredListingInvoiceId == sponsoredListingInvoiceId, ct);
        }

        public async Task<List<AffiliateCommission>> ListForAffiliateAsync(
            int affiliateAccountId,
            CancellationToken ct = default)
        {
            return await this.context.AffiliateCommissions
                .AsNoTracking()
                .Where(c => c.AffiliateAccountId == affiliateAccountId)
                .OrderByDescending(c => c.CreateDate)
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(CommissionPayoutStatus status, CancellationToken ct = default) =>
            this.context.AffiliateCommissions
            .AsNoTracking()
            .Where(c => c.PayoutStatus == status)
            .CountAsync(ct);
    }
}