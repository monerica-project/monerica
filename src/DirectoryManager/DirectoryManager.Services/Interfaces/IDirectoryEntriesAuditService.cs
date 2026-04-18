using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Services.Interfaces
{
    public interface IDirectoryEntriesAuditService
    {
        public Task<List<MonthlyAuditReport>> GetMonthlyReportAsync();
    }
}