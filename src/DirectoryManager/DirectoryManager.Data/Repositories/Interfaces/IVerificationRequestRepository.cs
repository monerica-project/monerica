using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IVerificationRequestRepository
    {
        Task AddAsync(VerificationRequest entity, CancellationToken ct = default);

        Task<VerificationRequest?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<VerificationRequest?> GetByTokenAsync(Guid token, CancellationToken ct = default);

        Task SetInvoiceIdAsync(int id, string invoiceId, CancellationToken ct = default);

        Task SetPaidAsync(int id, decimal? amount, string? currency, DateTime paidUtc, CancellationToken ct = default);

        Task<List<VerificationRequest>> ListByStatusAsync(VerificationRequestStatus status, int page, int pageSize, CancellationToken ct = default);

        Task<int> CountByStatusAsync(VerificationRequestStatus status, CancellationToken ct = default);

        Task<DateTime?> GetLastSubmissionUtcAsync(CancellationToken ct = default);

        Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default);

        Task SetStatusAsync(int id, VerificationRequestStatus status, CancellationToken ct = default);
    }
}
