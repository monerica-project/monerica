using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubmissionRepository
    {
        Task<IEnumerable<Submission>> GetAllAsync();
        Task<Submission?> GetByIdAsync(int submissionId);
        Task<Submission?> GetByLinkAndStatusAsync(string link1, SubmissionStatus submissionStatus = SubmissionStatus.Pending);

        /// <summary>
        /// True when a PENDING submission for this entry (by entry id or link) already has a note
        /// containing <paramref name="noteFragment"/> — e.g. an automated "site offline" proposal.
        /// </summary>
        Task<bool> HasPendingWithNoteContainingAsync(int directoryEntryId, string? link, string noteFragment);

        /// <summary>Submissions for an entry in the given status, newest first.</summary>
        Task<List<Submission>> ListByDirectoryEntryAndStatusAsync(int directoryEntryId, SubmissionStatus submissionStatus);
        Task<Submission> CreateAsync(Submission submission);
        Task UpdateAsync(Submission submission);
        Task DeleteAsync(int submissionId);
        Task<int> GetByStatus(SubmissionStatus pending);

        // ---- Optional review-donation flow (mirrors the verification-request flow) ----
        Task<Submission?> GetByPaymentTokenAsync(Guid token);
        Task SetInvoiceIdAsync(int submissionId, string invoiceId);
        Task SetPaidAsync(int submissionId, decimal? amount, string? currency, decimal? paidUsd, DateTime paidUtc);

        /// <summary>Id of the not-yet-paid record that owns this BTCPay donation invoice, or null.</summary>
        Task<int?> GetUnpaidIdByInvoiceIdAsync(string invoiceId, CancellationToken ct = default);

        /// <summary>BTCPay invoice ids of donations not yet marked paid (all when createdSinceUtc is null).</summary>
        Task<List<string>> GetUnpaidInvoiceIdsAsync(DateTime? createdSinceUtc, CancellationToken ct = default);

        /// <summary>Invoice ids of paid donations whose XMR amount isn't recorded yet.</summary>
        Task<List<string>> GetPaidInvoiceIdsMissingXmrAsync(CancellationToken ct = default);

        /// <summary>Stores the XMR actually received on the paid donation with this invoice id.</summary>
        /// <returns>True when a row was updated.</returns>
        Task<bool> SetPaidXmrByInvoiceIdAsync(string invoiceId, decimal paidXmr, CancellationToken ct = default);

        /// <summary>Newest submission's create time (any status), or null.</summary>
        Task<DateTime?> GetLastSubmissionUtcAsync(CancellationToken ct = default);

        /// <summary>Create time of the newest submission still Pending, or null.</summary>
        Task<DateTime?> GetNewestPendingUtcAsync(CancellationToken ct = default);

        /// <summary>Most recent donation payment time, or null.</summary>
        Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default);
    }
}