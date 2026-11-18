using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
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

        Task<IReadOnlyDictionary<Currency, decimal>> GetCurrencyTotalsAsync();

        Task<IReadOnlyDictionary<Currency, decimal>> GetCurrencyTotalsByDirectoryEntryAsync(int directoryEntryId);

        Task<IReadOnlyDictionary<Currency, decimal>> GetCurrencyTotalsByDateRangeAsync(
            DateTime startDate,
            DateTime endDate);

        Task<IEnumerable<AffiliateCommissionEarnedTotal>> GetTotalsByDirectoryEntryAsync();

        Task<IEnumerable<AffiliateCommissionEarnedTotal>> GetTotalsByDirectoryEntryAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Commission rows (entry id + name, date, USD) with CommissionDate in [fromInclusive, toExclusive),
        /// optionally for one listing. Null bounds are open.
        /// </summary>
        Task<List<AffiliateCommissionEarnedRow>> GetRowsAsync(
            DateTime? fromInclusive,
            DateTime? toExclusive,
            int? directoryEntryId = null);

        /// <summary>Earliest CommissionDate on record, or null when there are none.</summary>
        Task<DateTime?> GetFirstCommissionDateAsync();

        Task<AffiliateCommissionEarned> CreateAsync(AffiliateCommissionEarned commission);

        Task UpdateAsync(AffiliateCommissionEarned commission);

        Task DeleteAsync(int affiliateCommissionId);

        Task<bool> ExistsAsync(int affiliateCommissionId);
    }
}
