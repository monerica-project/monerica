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

        Task SetPaidAsync(int id, decimal? amount, string? currency, decimal? paidUsd, DateTime paidUtc, CancellationToken ct = default);

        /// <summary>Id of the not-yet-paid record that owns this BTCPay donation invoice, or null.</summary>
        Task<int?> GetUnpaidIdByInvoiceIdAsync(string invoiceId, CancellationToken ct = default);

        /// <summary>BTCPay invoice ids of donations not yet marked paid (all when createdSinceUtc is null).</summary>
        Task<List<string>> GetUnpaidInvoiceIdsAsync(DateTime? createdSinceUtc, CancellationToken ct = default);

        /// <summary>Invoice ids of paid donations whose XMR amount isn't recorded yet.</summary>
        Task<List<string>> GetPaidInvoiceIdsMissingXmrAsync(CancellationToken ct = default);

        /// <summary>Stores the XMR actually received on the paid donation with this invoice id.</summary>
        /// <returns>True when a row was updated.</returns>
        Task<bool> SetPaidXmrByInvoiceIdAsync(string invoiceId, decimal paidXmr, CancellationToken ct = default);

        Task<List<VerificationRequest>> ListByStatusAsync(VerificationRequestStatus status, int page, int pageSize, CancellationToken ct = default);

        Task<int> CountByStatusAsync(VerificationRequestStatus status, CancellationToken ct = default);

        Task<DateTime?> GetLastSubmissionUtcAsync(CancellationToken ct = default);

        /// <summary>Create time of the newest item still Pending (not yet handled), or null.</summary>
        Task<DateTime?> GetNewestPendingUtcAsync(CancellationToken ct = default);

        Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default);

        Task SetStatusAsync(int id, VerificationRequestStatus status, CancellationToken ct = default);
    }
}
