using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// "Reject with a reason" for a single submission: the Reject button on the review page opens
    /// a page of common reasons (radio buttons, plus "Other"). The chosen reason is recorded in the
    /// submission's review log, the same place the automated-review page's Reject records it.
    /// Nothing is sent to the submitter (same as every other reject path).
    /// </summary>
    public partial class SubmissionController
    {
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpGet("submission/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, string? from = null)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            return this.View("Reject", BuildRejectViewModel(submission, from, SubmissionDecision.SuggestReasonKey(submission.ReviewPrefilterReason), null, null));
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost("submission/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPost(int id, string? reason, string? otherReason, string? from)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            if (submission.SubmissionStatus != SubmissionStatus.Pending)
            {
                var vm = BuildRejectViewModel(submission, from, reason, otherReason, null);
                vm.Error = $"This submission is {submission.SubmissionStatus}, not Pending, so it was not changed.";
                return this.View("Reject", vm);
            }

            var reasonText = SubmissionDecision.ResolveReason(reason, otherReason, out var error);
            if (reasonText == null)
            {
                var vm = BuildRejectViewModel(submission, from, reason, otherReason, null);
                vm.Error = error;
                return this.View("Reject", vm);
            }

            // Same bookkeeping as the other reject paths (review form, automated-review page).
            submission.OriginalSnapshotJson ??= submission.CreateSnapshotJson();
            submission.SubmissionStatus = SubmissionDecision.StatusFor(reason);
            submission.ReviewState = SubmissionReviewState.Rejected;
            submission.AppendReviewLog($"Admin {this.User?.Identity?.Name}: rejected ({reasonText}).");
            await this.submissionRepository.UpdateAsync(submission);

            var backTo = SubmissionDecision.NormalizeFrom(from) ?? SubmissionDecision.FromPending;
            this.TempData[SubmissionDecision.MessageKey(backTo)] = $"Rejected \"{submission.Name}\" (#{id}): {reasonText}";
            return this.LocalRedirect(SubmissionDecision.ReturnPath(backTo));
        }

        private static SubmissionRejectViewModel BuildRejectViewModel(Submission submission, string? from, string? reason, string? otherReason, string? error)
        {
            bool reviewerSaysReject = submission.ReviewState is SubmissionReviewState.AutoRejected or SubmissionReviewState.AiRejectCandidate;
            return new SubmissionRejectViewModel
            {
                Submission = submission,
                From = SubmissionDecision.NormalizeFrom(from),
                Reason = reason,
                OtherReason = otherReason,
                Error = error,
                SuggestedByReviewer = !string.IsNullOrWhiteSpace(submission.ReviewPrefilterReason)
                    ? submission.ReviewPrefilterReason
                    : reviewerSaysReject ? submission.ReviewSummary : null,
            };
        }
    }
}
