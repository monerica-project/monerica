using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// "Submit a link": the owner pastes one address and a pending submission is created from it.
    /// The automated review then reads the site and drafts the name, description, category and
    /// tags, and the result waits on the AI-reviewed submissions page like any other submission.
    /// </summary>
    public partial class SubmissionController
    {
        private const string FromLinkPath = "admin/submission-from-link";

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpGet(FromLinkPath)]
        public IActionResult FromLink() => this.View("FromLink", new SubmissionFromLinkModel());

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost(FromLinkPath)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FromLink(SubmissionFromLinkModel model, CancellationToken ct)
        {
            if (!LinkOnlySubmission.TryParse(model.Link, out var link, out var proofLink, out var error))
            {
                model.Problem = error;
                return this.View("FromLink", model);
            }

            // Already listed, or already waiting for review: say so and point there instead of adding a duplicate.
            if (await this.FindListingForLinkAsync(link) is DirectoryEntry listed)
            {
                model.Problem = $"{listed.Name} is already listed (#{listed.DirectoryEntryId}).";
                model.ProblemUrl = "/directoryentry/edit/" + listed.DirectoryEntryId;
                model.ProblemUrlText = "Open the listing";
                return this.View("FromLink", model);
            }

            var waiting = await this.submissionRepository.GetByLinkAndStatusAsync(link)
                ?? await this.submissionRepository.GetByLinkAndStatusAsync(link.TrimEnd('/'));
            if (waiting != null)
            {
                model.Problem = $"A submission for this site is already waiting for review (#{waiting.SubmissionId}).";
                model.ProblemUrl = "/submission/" + waiting.SubmissionId;
                model.ProblemUrlText = "Open the submission";
                return this.View("FromLink", model);
            }

            var note = (model.Note ?? string.Empty).Trim();
            var noteToAdmin = LinkOnlySubmission.NoteText + (note.Length > 0 ? " Owner's note: " + note : string.Empty);
            var submission = new Submission
            {
                SubmissionStatus = SubmissionStatus.Pending,
                Name = LinkOnlySubmission.PlaceholderName(link),
                Link = link,
                ProofLink = proofLink,
                NoteToAdmin = noteToAdmin.Length > 1000 ? noteToAdmin[..1000] : noteToAdmin,
                DirectoryStatus = DirectoryStatus.Admitted,
                PaymentToken = Guid.NewGuid(),
                SubmittedUtc = DateTime.UtcNow,
            };

            var saved = await this.submissionRepository.CreateAsync(submission);
            this.TempData["AiReviewMessage"] =
                $"Submission #{saved.SubmissionId} created for {saved.Name}. The automated review fills in the details within about 10 minutes; it will then show here.";
            return this.Redirect("/" + ReviewPagePath);
        }
    }
}
