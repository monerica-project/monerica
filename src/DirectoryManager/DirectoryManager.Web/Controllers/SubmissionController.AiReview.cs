using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Models.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin page for the automated submission reviewer (DirectoryManager.SubmissionReviewer).
    /// Lives in SubmissionController so approving uses exactly the same listing create/update code
    /// as the regular review form. The submitted fields are never overwritten here: a one-click
    /// approve publishes the reviewer's suggested (Proposed*) version and keeps the original.
    /// </summary>
    public partial class SubmissionController
    {
        private const string ReviewPagePath = "admin/submission-review";

        [Authorize]
        [HttpGet(ReviewPagePath)]
        public async Task<IActionResult> AiReview(CancellationToken ct)
        {
            var subs = await this.submissionRepository.ListPendingForReviewAsync(ct);
            var subcats = await this.subCategoryRepository.GetAllDtoAsync();
            var config = this.HttpContext.RequestServices.GetService<IConfiguration>();
            var model = new SubmissionAiReviewPageModel
            {
                Submissions = subs,
                SubcategoryLabels = subcats.ToDictionary(s => s.SubcategoryId, s => $"{s.CategoryName} > {s.Name}"),
                AiReviewsToday = await this.submissionRepository.CountAiReviewsSinceAsync(DateTime.UtcNow.Date, ct),
                DailyCap = int.TryParse(config?["SubmissionReviewer:DailyCap"], out var cap) ? cap : 50,
                Message = this.TempData["AiReviewMessage"] as string,
            };

            return this.View("AiReview", model);
        }

        [Authorize]
        [HttpPost(ReviewPagePath + "/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewApprove(int id, CancellationToken ct)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            var error = await this.ApproveSuggestedVersionAsync(submission, ct);
            this.TempData["AiReviewMessage"] = error == null
                ? $"Approved \"{submission.ProposedName ?? submission.Name}\"."
                : $"Could not approve #{id}: {error}";
            return this.Redirect("~/" + ReviewPagePath);
        }

        [Authorize]
        [HttpPost(ReviewPagePath + "/approve-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewApproveAll(CancellationToken ct)
        {
            var subs = (await this.submissionRepository.ListPendingForReviewAsync(ct))
                .Where(s => s.ReviewState == SubmissionReviewState.AiApproveCandidate)
                .ToList();

            int ok = 0;
            var failed = new List<string>();
            foreach (var s in subs)
            {
                var error = await this.ApproveSuggestedVersionAsync(s, ct);
                if (error == null)
                {
                    ok++;
                }
                else
                {
                    failed.Add($"#{s.SubmissionId} {s.Name}: {error}");
                }
            }

            this.TempData["AiReviewMessage"] = $"Approved {ok} of {subs.Count}." +
                (failed.Count == 0 ? string.Empty : " Not approved: " + string.Join("; ", failed));
            return this.Redirect("~/" + ReviewPagePath);
        }

        [Authorize]
        [HttpPost(ReviewPagePath + "/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewReject(int id)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            await this.RejectFromReviewAsync(submission);
            this.TempData["AiReviewMessage"] = $"Rejected \"{submission.Name}\".";
            return this.Redirect("~/" + ReviewPagePath);
        }

        [Authorize]
        [HttpPost(ReviewPagePath + "/reject-auto")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewRejectAutoRejected(CancellationToken ct)
        {
            var subs = (await this.submissionRepository.ListPendingForReviewAsync(ct))
                .Where(s => s.ReviewState == SubmissionReviewState.AutoRejected)
                .ToList();
            foreach (var s in subs)
            {
                await this.RejectFromReviewAsync(s);
            }

            this.TempData["AiReviewMessage"] = $"Rejected {subs.Count} auto-rejected submission(s).";
            return this.Redirect("~/" + ReviewPagePath);
        }

        [Authorize]
        [HttpPost(ReviewPagePath + "/{id:int}/rerun")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewRerun(int id)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            submission.ReviewState = SubmissionReviewState.Pending;
            submission.ReviewError = null;
            submission.AppendReviewLog($"Admin {this.User?.Identity?.Name}: re-run review requested.");
            await this.submissionRepository.UpdateAsync(submission);
            this.TempData["AiReviewMessage"] = $"#{id} queued for another review (runs within 10 minutes).";
            return this.Redirect("~/" + ReviewPagePath);
        }

        /// <summary>The submission with the reviewer's suggested values applied (in memory; nothing saved).</summary>
        internal static Submission WithProposal(Submission s)
        {
            var p = s.ShallowCopy();
            p.Name = string.IsNullOrWhiteSpace(s.ProposedName) ? s.Name : s.ProposedName.Trim();
            p.Description = s.ProposedDescription ?? s.Description;
            p.Note = s.ProposedNote;
            p.SubCategoryId = s.ProposedSubCategoryId ?? s.SubCategoryId;
            p.CountryCode = s.ProposedCountryCode ?? s.CountryCode;
            p.StreetAddress = s.ProposedStreetAddress ?? s.StreetAddress;
            p.City = s.ProposedCity ?? s.City;
            p.StateRegion = s.ProposedStateRegion ?? s.StateRegion;
            p.PostalCode = s.ProposedPostalCode ?? s.PostalCode;
            p.KycPolicy = s.ProposedKycPolicy ?? s.KycPolicy;
            p.Liquidity = s.ProposedLiquidity ?? s.Liquidity;
            p.RateType = s.ProposedRateType ?? s.RateType;
            p.SiteJavaScript = s.ProposedSiteJavaScript ?? s.SiteJavaScript;
            p.Tags = s.ProposedTags ?? s.Tags;
            return p;
        }

        /// <summary>Approves the suggested version. Returns null on success, else why it couldn't.</summary>
        private async Task<string?> ApproveSuggestedVersionAsync(Submission submission, CancellationToken ct)
        {
            if (submission.SubmissionStatus != SubmissionStatus.Pending)
            {
                return $"it is {submission.SubmissionStatus}, not Pending";
            }

            if (submission.ProposedName == null && submission.ReviewModel == null)
            {
                return "it has no reviewed version yet";
            }

            var posted = WithProposal(submission);
            posted.SubmissionStatus = SubmissionStatus.Approved;

            // New listings default to Admitted; an edit keeps the listing's status unless the submission set one.
            if (posted.DirectoryStatus == DirectoryStatus.Unknown)
            {
                posted.DirectoryStatus = submission.DirectoryEntryId == null ? DirectoryStatus.Admitted : null;
            }

            if (string.IsNullOrWhiteSpace(posted.Name))
            {
                return "no name";
            }

            if (posted.SubCategoryId is null or 0)
            {
                return "no subcategory (use Edit then approve)";
            }

            if ((posted.Description?.Length ?? 0) > ListingText.DescriptionMaxLength)
            {
                return $"description is {posted.Description!.Length} characters (limit {ListingText.DescriptionMaxLength})";
            }

            if ((posted.Note?.Length ?? 0) > ListingText.NoteMaxLength)
            {
                return $"note is {posted.Note!.Length} characters (limit {ListingText.NoteMaxLength})";
            }

            submission.OriginalSnapshotJson ??= submission.CreateSnapshotJson();
            var tags = ParseCsvIds(submission.SelectedTagIdsCsv).ToArray();
            try
            {
                await this.ApproveIfNeededAsync(submission, posted, tags, submission.RelatedLinks, ct);
            }
            catch (Exception ex)
            {
                return ex.GetBaseException().Message;
            }

            submission.SubmissionStatus = SubmissionStatus.Approved;
            submission.ReviewState = SubmissionReviewState.Approved;
            submission.AppendReviewLog($"Admin {this.User?.Identity?.Name}: approved the suggested version → listing #{submission.DirectoryEntryId}.");
            await this.submissionRepository.UpdateAsync(submission);
            return null;
        }

        private async Task RejectFromReviewAsync(Submission submission)
        {
            submission.OriginalSnapshotJson ??= submission.CreateSnapshotJson();
            submission.SubmissionStatus = SubmissionStatus.Rejected;
            submission.ReviewState = SubmissionReviewState.Rejected;
            submission.AppendReviewLog($"Admin {this.User?.Identity?.Name}: rejected ({submission.ReviewPrefilterReason ?? submission.ReviewVerdict ?? "no reason recorded"}).");
            await this.submissionRepository.UpdateAsync(submission);
        }
    }
}
