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
                                 // Automated proposals put their reason in NoteToAdmin (the public
                                 // Note is copied from the listing), so look in both.
                                 ((x.Note != null && x.Note.Contains(noteFragment)) ||
                                  (x.NoteToAdmin != null && x.NoteToAdmin.Contains(noteFragment))));
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

        public Task<DateTime?> GetLastSubmissionUtcAsync(CancellationToken ct = default) =>
            this.context.Submissions.AsNoTracking()
                .OrderByDescending(s => s.CreateDate)
                .Select(s => (DateTime?)s.CreateDate)
                .FirstOrDefaultAsync(ct);

        public Task<DateTime?> GetNewestPendingUtcAsync(CancellationToken ct = default) =>
            this.context.Submissions.AsNoTracking()
                .Where(s => s.SubmissionStatus == SubmissionStatus.Pending)
                .OrderByDescending(s => s.CreateDate)
                .Select(s => (DateTime?)s.CreateDate)
                .FirstOrDefaultAsync(ct);

        public Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default) =>
            this.context.Submissions.AsNoTracking()
                .Where(s => s.PaidUtc != null)
                .OrderByDescending(s => s.PaidUtc)
                .Select(s => s.PaidUtc)
                .FirstOrDefaultAsync(ct);

        public Task<List<string>> GetPaidInvoiceIdsMissingXmrAsync(CancellationToken ct = default) =>
            this.context.Submissions.AsNoTracking()
                .Where(x => x.BtcPayInvoiceId != null && x.BtcPayInvoiceId != string.Empty && x.PaidUtc != null && x.PaidXmr == null)
                .Select(x => x.BtcPayInvoiceId!)
                .ToListAsync(ct);

        public async Task<bool> SetPaidXmrByInvoiceIdAsync(string invoiceId, decimal paidXmr, CancellationToken ct = default)
        {
            var row = await this.context.Submissions.FirstOrDefaultAsync(x => x.BtcPayInvoiceId == invoiceId && x.PaidUtc != null, ct);
            if (row is null || paidXmr <= 0 || row.PaidXmr == paidXmr)
            {
                return false;
            }

            row.PaidXmr = paidXmr;
            await this.context.SaveChangesAsync(ct);
            return true;
        }

        public Task<List<string>> GetUnpaidInvoiceIdsAsync(DateTime? createdSinceUtc, CancellationToken ct = default) =>
            this.context.Submissions.AsNoTracking()
                .Where(x => x.BtcPayInvoiceId != null && x.BtcPayInvoiceId != string.Empty && x.PaidUtc == null)
                .Where(x => createdSinceUtc == null || x.CreateDate >= createdSinceUtc)
                .OrderByDescending(x => x.CreateDate)
                .Select(x => x.BtcPayInvoiceId!)
                .ToListAsync(ct);

        public Task<int?> GetUnpaidIdByInvoiceIdAsync(string invoiceId, CancellationToken ct = default) =>
            this.context.Submissions.AsNoTracking()
                .Where(x => x.BtcPayInvoiceId == invoiceId && x.PaidUtc == null)
                .Select(x => (int?)x.SubmissionId)
                .FirstOrDefaultAsync(ct);
    }
}