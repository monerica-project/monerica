using DirectoryManager.Data.Models;
namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntriesAuditRepository
    {
        Task<IEnumerable<DirectoryEntriesAudit>> GetAllAsync();
        Task CreateAsync(DirectoryEntriesAudit directoryEntriesAudit);
        Task<IEnumerable<DirectoryEntriesAudit>> GetAuditsWithSubCategoriesForEntryAsync(int entryId);
        Task<List<DirectoryEntriesAudit>> GetAllWithSubcategoriesAsync(DateTime fromUtc, DateTime toUtc);

        /// <summary>
        /// Listings whose current status is Scam or Questionable (open issues) with their public
        /// facts, plus their status history as narrow audit rows.
        /// </summary>
        Task<(List<DirectoryManager.Data.Models.TransferModels.AuditStatusRow> Audits, List<DirectoryManager.Data.Models.TransferModels.IncidentListingRow> Listings)> GetOpenIssueHistoryAsync(CancellationToken ct = default);
    }
}