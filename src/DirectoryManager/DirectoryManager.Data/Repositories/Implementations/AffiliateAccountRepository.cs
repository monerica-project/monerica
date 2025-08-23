// DirectoryManager.Data/Repositories/Implementations/AffiliateAccountRepository.cs
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class AffiliateAccountRepository : IAffiliateAccountRepository
    {
        private readonly IApplicationDbContext context;
        private DbSet<AffiliateAccount> Set => this.context.AffiliateAccounts;

        public AffiliateAccountRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<AffiliateAccount?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.AsNoTracking().FirstOrDefaultAsync(a => a.AffiliateAccountId == id, ct);

        public async Task<AffiliateAccount?> GetByReferralCodeAsync(string referralCode, CancellationToken ct = default)
        {
            referralCode = referralCode?.Trim() ?? string.Empty;
            return await this.Set.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ReferralCode == referralCode, ct);
        }

        public async Task<AffiliateAccount?> GetByCodeAndWalletAsync(string referralCode, string walletAddress, CancellationToken ct = default)
        {
            referralCode = referralCode?.Trim() ?? string.Empty;
            walletAddress = walletAddress?.Trim() ?? string.Empty;

            return await this.Set.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ReferralCode == referralCode && a.WalletAddress == walletAddress, ct);
        }

        public async Task<bool> ExistsByReferralCodeAsync(string referralCode, CancellationToken ct = default)
        {
            referralCode = referralCode?.Trim() ?? string.Empty;
            return await this.Set.AnyAsync(a => a.ReferralCode == referralCode, ct);
        }

        public async Task<AffiliateAccount> CreateAsync(AffiliateAccount entity, CancellationToken ct = default)
        {
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
            return entity;
        }

        public async Task UpdateAsync(AffiliateAccount entity, CancellationToken ct = default)
        {
            this.Set.Update(entity);
            await this.context.SaveChangesAsync(ct);
        }


        public async Task<List<AffiliateAccount>> ListAllAsync(CancellationToken ct)
        {
            return await this.context.AffiliateAccounts.AsNoTracking()
            .OrderBy(a => a.ReferralCode)
            .ToListAsync(ct);
        }
    }
}
