using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SiteActivityRepository : ISiteActivityRepository
    {
        // NoteToAdmin prefixes written by the unattended checkers (SiteChecker, DetailChecker).
        // Their submissions aren't made by people, so the activity report leaves them out.
        private const string SiteCheckerMarker = "(automated submission)";
        private const string DetailCheckerMarker = "[auto-detail-check";

        private readonly IApplicationDbContext context;

        public SiteActivityRepository(IApplicationDbContext context) => this.context = context;

        public async Task<List<SiteActivityItem>> GetActivityAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        {
            var items = new List<SiteActivityItem>();

            var submissions = await this.context.Submissions.AsNoTracking()
                .Where(s => s.CreateDate >= fromUtc && s.CreateDate < toUtc
                            && s.SubmissionStatus != SubmissionStatus.Preview
                            && (s.NoteToAdmin == null
                                || (!s.NoteToAdmin.StartsWith(SiteCheckerMarker) && !s.NoteToAdmin.StartsWith(DetailCheckerMarker))))
                .Select(s => new
                {
                    s.DirectoryEntryId,
                    s.CreateDate,
                    EntryCreated = this.context.DirectoryEntries
                        .Where(d => d.DirectoryEntryId == s.DirectoryEntryId)
                        .Select(d => (DateTime?)d.CreateDate)
                        .FirstOrDefault(),
                })
                .ToListAsync(ct);

            // Approving a NEW listing submission links it to the listing it created, so "has a
            // DirectoryEntryId" doesn't mean "edit". It's an edit only when the listing already
            // existed before the submission was made.
            items.AddRange(submissions.Select(s => new SiteActivityItem(
                IsEdit(s.DirectoryEntryId, s.EntryCreated, s.CreateDate) ? SiteActivityType.ListingEdit : SiteActivityType.NewListing,
                s.CreateDate)));

            items.AddRange((await this.context.DirectoryEntryReviews.AsNoTracking()
                    .Where(r => r.CreateDate >= fromUtc && r.CreateDate < toUtc)
                    .Select(r => r.CreateDate)
                    .ToListAsync(ct))
                .Select(d => new SiteActivityItem(SiteActivityType.Review, d)));

            items.AddRange((await this.context.DirectoryEntryReviewComments.AsNoTracking()
                    .Where(c => c.CreateDate >= fromUtc && c.CreateDate < toUtc)
                    .Select(c => c.CreateDate)
                    .ToListAsync(ct))
                .Select(d => new SiteActivityItem(SiteActivityType.ReviewReply, d)));

            items.AddRange((await this.context.ProblemReports.AsNoTracking()
                    .Where(p => p.CreateDate >= fromUtc && p.CreateDate < toUtc)
                    .Select(p => p.CreateDate)
                    .ToListAsync(ct))
                .Select(d => new SiteActivityItem(SiteActivityType.ProblemReport, d)));

            items.AddRange((await this.context.VerificationRequests.AsNoTracking()
                    .Where(v => v.CreateDate >= fromUtc && v.CreateDate < toUtc)
                    .Select(v => v.CreateDate)
                    .ToListAsync(ct))
                .Select(d => new SiteActivityItem(SiteActivityType.VerificationRequest, d)));

            return items;
        }

        /// <summary>
        /// An edit targets a listing that existed before the submission. No listing, or a listing
        /// created at/after the submission (an approved new-listing submission), means a new listing.
        /// If the listing has since been deleted, a linked submission still counts as an edit.
        /// </summary>
        /// <returns>True for an edit of an existing listing.</returns>
        public static bool IsEdit(int? directoryEntryId, DateTime? entryCreated, DateTime submissionCreated) =>
            directoryEntryId != null && (entryCreated == null || entryCreated < submissionCreated);
    }
}
