using DirectoryManager.Data.Models.Affiliates;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAffiliateAccountRepository
    {
        Task<AffiliateAccount?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<AffiliateAccount?> GetByReferralCodeAsync(string referralCode, CancellationToken ct = default);
        Task<AffiliateAccount?> GetByCodeAndWalletAsync(string referralCode, string walletAddress, CancellationToken ct = default);
        Task<bool> ExistsByReferralCodeAsync(string referralCode, CancellationToken ct = default);

        Task<AffiliateAccount> CreateAsync(AffiliateAccount entity, CancellationToken ct = default);
        Task UpdateAsync(AffiliateAccount entity, CancellationToken ct = default);
        Task<List<AffiliateAccount>> ListAllAsync(CancellationToken ct);
    }
}