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
    }
}