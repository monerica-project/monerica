using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubmissionRepository
    {
        Task<IEnumerable<Submission>> GetAllAsync();
        Task<Submission?> GetByIdAsync(int submissionId);
        Task<Submission?> GetByLinkAndStatusAsync(string link1, SubmissionStatus submissionStatus = SubmissionStatus.Pending);
        Task<Submission> CreateAsync(Submission submission);
        Task UpdateAsync(Submission submission);
        Task DeleteAsync(int submissionId);
        Task<int> GetByStatus(SubmissionStatus pending);

        // ---- Optional review-donation flow (mirrors the verification-request flow) ----
        Task<Submission?> GetByPaymentTokenAsync(Guid token);
        Task SetInvoiceIdAsync(int submissionId, string invoiceId);
        Task SetPaidAsync(int submissionId, decimal? amount, string? currency);
    }
}