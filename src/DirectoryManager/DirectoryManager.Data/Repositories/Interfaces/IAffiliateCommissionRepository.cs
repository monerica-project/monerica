// DirectoryManager.Data/Repositories/Interfaces/IAffiliateCommissionRepository.cs
using DirectoryManager.Data.Models.Affiliates;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAffiliateCommissionRepository
    {
        Task<AffiliateCommission?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<AffiliateCommission?> GetByInvoiceIdAsync(int sponsoredListingInvoiceId, CancellationToken ct = default);

        Task<AffiliateCommission> AddAsync(AffiliateCommission entity, CancellationToken ct = default);
        Task UpdateAsync(AffiliateCommission entity, CancellationToken ct = default);
        Task<List<AffiliateCommission>> ListForAffiliateAsync(int affiliateAccountId, CancellationToken ct = default);
        Task<bool> ExistsForInvoiceAsync(int sponsoredListingInvoiceId, CancellationToken ct);
    }
}
