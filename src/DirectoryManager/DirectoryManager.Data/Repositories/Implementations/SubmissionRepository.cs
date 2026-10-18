using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubmissionRepository : ISubmissionRepository
    {
        private readonly IApplicationDbContext context;

        public SubmissionRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<Submission>> GetAllAsync()
        {
            return await this.context.Submissions
                                 .OrderByDescending(x => x.CreateDate)
                                 .ToListAsync();
        }

        public async Task<Submission?> GetByIdAsync(int submissionId)
        {
            return await this.context.Submissions.FindAsync(submissionId);
        }

        public async Task<Submission?> GetByLinkAndStatusAsync(string link1, SubmissionStatus submissionStatus = SubmissionStatus.Pending)
        {
            if (string.IsNullOrWhiteSpace(link1))
            {
                return default;
            }

            return await this.context
                             .Submissions
                             .Where(x => x.Link == link1 && x.SubmissionStatus == submissionStatus)
                             .FirstOrDefaultAsync();
        }

        public async Task<bool> HasPendingWithNoteContainingAsync(int directoryEntryId, string? link, string noteFragment)
        {
            if (string.IsNullOrEmpty(noteFragment))
            {
                return false;
            }

            var hasLink = !string.IsNullOrWhiteSpace(link);

            return await this.context
                             .Submissions
                             .AnyAsync(x =>
                                 x.SubmissionStatus == SubmissionStatus.Pending &&
                                 ((directoryEntryId > 0 && x.DirectoryEntryId == directoryEntryId) ||
                                  (hasLink && x.Link == link)) &&
                                 x.Note != null &&
                                 x.Note.Contains(noteFragment));
        }

        public async Task<List<Submission>> ListByDirectoryEntryAndStatusAsync(int directoryEntryId, SubmissionStatus submissionStatus)
        {
            return await this.context
                             .Submissions
                             .AsNoTracking()
                             .Where(x => x.DirectoryEntryId == directoryEntryId && x.SubmissionStatus == submissionStatus)
                             .OrderByDescending(x => x.CreateDate)
                             .ToListAsync();
        }

        public async Task<Submission> CreateAsync(Submission submission)
        {
            // Ensure only the ID is set for the foreign key, and EF does not track the SubCategory entity itself
            submission.SubCategory = null;
            submission.CreateDate = DateTime.UtcNow;
            await this.context.Submissions.AddAsync(submission);
            await this.context.SaveChangesAsync();

            return submission;
        }

        public async Task UpdateAsync(Submission submission)
        {
            this.context.Submissions.Update(submission);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int submissionId)
        {
            var submission = await this.GetByIdAsync(submissionId);
            if (submission != null)
            {
                this.context.Submissions.Remove(submission);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<int> GetByStatus(SubmissionStatus status)
        {
            return await this.context.Submissions
                                 .Where(x => x.SubmissionStatus == status)
                                 .CountAsync();
        }

        public async Task<Submission?> GetByPaymentTokenAsync(Guid token)
        {
            return await this.context.Submissions
                                 .FirstOrDefaultAsync(x => x.PaymentToken == token);
        }

        public async Task SetInvoiceIdAsync(int submissionId, string invoiceId)
        {
            var submission = await this.GetByIdAsync(submissionId);
            if (submission == null)
            {
                return;
            }

            submission.BtcPayInvoiceId = invoiceId;
            await this.context.SaveChangesAsync();
        }

        public async Task SetPaidAsync(int submissionId, decimal? amount, string? currency, decimal? paidUsd, DateTime paidUtc)
        {
            var submission = await this.GetByIdAsync(submissionId);
            if (submission == null || submission.PaidUtc != null)
            {
                return; // already recorded — don't overwrite
            }

            submission.PaidUtc = paidUtc;
            submission.PaidAmount = amount;
            submission.PaidCurrency = currency;
            submission.PaidUsd = paidUsd;
            await this.context.SaveChangesAsync();
        }
    }
}