using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.ProblemReports;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IProblemReportRepository
    {
        Task AddAsync(ProblemReport entity, CancellationToken ct = default);

        Task<ProblemReport?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<ProblemReport?> GetByTokenAsync(Guid token, CancellationToken ct = default);

        Task SetInvoiceIdAsync(int id, string invoiceId, CancellationToken ct = default);

        Task SetPaidAsync(int id, decimal? amount, string? currency, DateTime paidUtc, CancellationToken ct = default);

        Task<List<ProblemReport>> ListByStatusAsync(ProblemReportStatus status, int page, int pageSize, CancellationToken ct = default);

        Task<int> CountByStatusAsync(ProblemReportStatus status, CancellationToken ct = default);

        Task<DateTime?> GetLastSubmissionUtcAsync(CancellationToken ct = default);

        Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default);

        Task SetStatusAsync(int id, ProblemReportStatus status, CancellationToken ct = default);
    }
}
