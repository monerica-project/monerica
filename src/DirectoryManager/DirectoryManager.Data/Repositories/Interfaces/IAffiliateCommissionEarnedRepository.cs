using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IAffiliateCommissionEarnedRepository
    {
        Task<AffiliateCommissionEarned?> GetByIdAsync(int affiliateCommissionId);

        Task<IEnumerable<AffiliateCommissionEarned>> GetAllAsync();

        Task<(IEnumerable<AffiliateCommissionEarned> Items, int TotalCount)> GetPagedAsync(
            int page,
            int pageSize);

        Task<(IEnumerable<AffiliateCommissionEarned> Items, int TotalCount)> GetPagedByDirectoryEntryAsync(
            int directoryEntryId,
            int page,
            int pageSize);

        Task<(IEnumerable<AffiliateCommissionEarned> Items, int TotalCount)> GetPagedByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            int page,
            int pageSize);

        Task<decimal> GetTotalUsdValueAsync();

        Task<decimal> GetTotalUsdValueByDirectoryEntryAsync(int directoryEntryId);

        Task<decimal> GetTotalUsdValueByDateRangeAsync(DateTime startDate, DateTime endDate);

        Task<IEnumerable<AffiliateCommissionEarnedTotal>> GetTotalsByDirectoryEntryAsync();

        Task<IEnumerable<AffiliateCommissionEarnedTotal>> GetTotalsByDirectoryEntryAsync(
            DateTime startDate,
            DateTime endDate);

        Task<AffiliateCommissionEarned> CreateAsync(AffiliateCommissionEarned commission);

        Task UpdateAsync(AffiliateCommissionEarned commission);

        Task DeleteAsync(int affiliateCommissionId);

        Task<bool> ExistsAsync(int affiliateCommissionId);
    }
}
