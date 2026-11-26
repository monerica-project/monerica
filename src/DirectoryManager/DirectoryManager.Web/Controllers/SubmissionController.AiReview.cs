using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
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

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
            model.MessageIsProblem = model.Message != null
                && (model.Message.StartsWith("Could not", StringComparison.Ordinal) || model.Message.Contains("Not approved:", StringComparison.Ordinal));

            // Edits that touch links/contacts/key can't be approved in one click. Work that out now so
            // the page offers the full review form instead of a button that would only be refused.
            foreach (var s in subs.Where(x => x.DirectoryEntryId != null && x.ProposedName != null))
            {
                var entry = await this.directoryEntryRepository.GetByIdAsync(s.DirectoryEntryId!.Value);
                if (entry == null)
                {
                    continue;
                }

                var sensitive = SubmissionProtectedFields.SensitiveChanges(entry, OneClickVersion(s));
                if (sensitive.Count > 0)
                {
                    model.SensitiveChanges[s.SubmissionId] = sensitive;
                }
            }

            // The weekly site check's own submissions: say plainly what it found.
            var checkRepo = this.HttpContext.RequestServices.GetService<DirectoryManager.Data.Repositories.Interfaces.ISiteCheckStatusRepository>();
            foreach (var s in subs)
            {
                var finding = AutomatedSubmissionRefresh.Classify(s.NoteToAdmin, s.DirectoryStatus, s.Note);
                if (finding == SiteCheckFinding.None || s.DirectoryEntryId is not int checkedId)
                {
                    continue;
                }

                var entry = await this.directoryEntryRepository.GetByIdAsync(checkedId);
                var check = checkRepo == null ? null : await checkRepo.GetByDirectoryEntryIdAsync(checkedId, ct);
                model.SiteChecks[s.SubmissionId] = new SiteCheckRow
                {
                    Finding = finding,
                    CurrentStatus = entry?.DirectoryStatus,
                    OnionLink = finding == SiteCheckFinding.TorLinkOffline ? OnionLinkOf(entry) : null,
                    FailStreak = finding switch
                    {
                        SiteCheckFinding.MainSiteOffline => check?.ClearnetFailStreak ?? 0,
                        SiteCheckFinding.TorLinkOffline => check?.OnionFailStreak ?? 0,
                        _ => 0,
                    },
                    LastCheckedUtc = check?.LastCheckedUtc,
                    Reason = AutomatedSubmissionRefresh.OfflineReason(s.DirectoryStatus ?? DirectoryStatus.Unknown, s.Note),
                    Warning = AutomatedSubmissionRefresh.WarningText(s.NoteToAdmin),
                };
            }

            return this.View("AiReview", model);
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
            return this.Redirect("~/" + ReviewPagePath + "#result");
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
            return this.Redirect("~/" + ReviewPagePath + "#result");
        }

        /// <summary>Accepts the site check's finding: the listing is marked Removed with the offline reason in its note.</summary>
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost(ReviewPagePath + "/{id:int}/remove-offline")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewRemoveOffline(int id, CancellationToken ct)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            var error = await this.ApplySiteCheckAsync(submission, removeListing: true, ct);
            this.TempData["AiReviewMessage"] = error == null
                ? $"Removed \"{submission.Name}\" as offline."
                : $"Could not remove #{id}: {error}";
            return this.Redirect("~/" + ReviewPagePath + "#result");
        }

        /// <summary>Removes every listing whose MAIN site the weekly check found offline.</summary>
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost(ReviewPagePath + "/remove-offline-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewRemoveOfflineAll(CancellationToken ct)
        {
            var subs = (await this.submissionRepository.ListPendingForReviewAsync(ct))
                .Where(s => AutomatedSubmissionRefresh.Classify(s.NoteToAdmin, s.DirectoryStatus, s.Note) == SiteCheckFinding.MainSiteOffline)
                .ToList();

            int ok = 0;
            var failed = new List<string>();
            foreach (var s in subs)
            {
                var error = await this.ApplySiteCheckAsync(s, removeListing: true, ct);
                if (error == null)
                {
                    ok++;
                }
                else
                {
                    failed.Add($"#{s.SubmissionId} {s.Name}: {error}");
                }
            }

            this.TempData["AiReviewMessage"] = $"Removed {ok} of {subs.Count} offline listing(s)." +
                (failed.Count == 0 ? string.Empty : " Could not remove: " + string.Join("; ", failed));
            return this.Redirect("~/" + ReviewPagePath + "#result");
        }

        /// <summary>Only the Tor address is down: take that address off the listing and keep the listing.</summary>
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost(ReviewPagePath + "/{id:int}/remove-tor-link")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReviewRemoveTorLink(int id, CancellationToken ct)
        {
            var submission = await this.submissionRepository.GetByIdAsync(id);
            if (submission == null)
            {
                return this.NotFound();
            }

            var error = await this.ApplySiteCheckAsync(submission, removeListing: false, ct);
            this.TempData["AiReviewMessage"] = error == null
                ? $"Removed the Tor link from \"{submission.Name}\". The listing stays."
                : $"Could not change #{id}: {error}";
            return this.Redirect("~/" + ReviewPagePath + "#result");
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
            p.Social = s.ProposedSocial ?? s.Social;
            p.Messenger = s.ProposedMessenger ?? s.Messenger;
            p.Tags = s.ProposedTags ?? s.Tags;

            // PGP key found on the site: only fills an empty key, and only if it parses as a real key.
            // (Replacing an existing listing key is still blocked by SubmissionProtectedFields.)
            if (string.IsNullOrWhiteSpace(s.PgpKey) && SuggestedPgpFingerprint(s) != null)
            {
                p.PgpKey = s.ProposedPgpKey!.Trim();
            }

            return p;
        }

        /// <summary>What a one-click approval would publish (in memory; nothing saved).</summary>
        internal static Submission OneClickVersion(Submission submission)
        {
            var posted = WithProposal(submission);
            posted.SubmissionStatus = SubmissionStatus.Approved;

            // One-click approval never sets a status: new listings are Admitted, edits keep the
            // listing's current status. Anything else (Verified, Scam, ...) is an admin decision made
            // on the full Review page. (Audit 2026-11-11.)
            posted.DirectoryStatus = submission.DirectoryEntryId == null ? DirectoryStatus.Admitted : null;
            return posted;
        }

        /// <summary>Primary fingerprint of the reviewer's suggested PGP key, or null if there is none or it doesn't parse.</summary>
        internal static string? SuggestedPgpFingerprint(Submission s) =>
            string.IsNullOrWhiteSpace(s.ProposedPgpKey) ? null : Helpers.PgpCapabilities.GetPrimaryFingerprint(s.ProposedPgpKey);

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

            var posted = OneClickVersion(submission);

            // Edits that touch the listing's identity (main link, contact email, PGP key, status)
            // are never approved in one click: the full Review page shows them as warnings.
            if (submission.DirectoryEntryId is int entryId)
            {
                var entry = await this.directoryEntryRepository.GetByIdAsync(entryId);
                if (entry == null)
                {
                    return "the listing it edits no longer exists";
                }

                var sensitive = SubmissionProtectedFields.SensitiveChanges(entry, posted);
                if (sensitive.Count > 0)
                {
                    return "it changes sensitive fields (" + string.Join("; ", sensitive) + "); open the full Review page to approve it";
                }
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

        /// <summary>The listing's Tor address (.onion) from link 2 or link 3, if it has one.</summary>
        internal static string? OnionLinkOf(DirectoryEntry? entry) =>
            new[] { entry?.Link2, entry?.Link3 }.FirstOrDefault(l => UrlHelper.IsOnion(l));

        /// <summary>
        /// Applies a weekly site-check finding to the listing as it is NOW (the submission's copy is
        /// refreshed from the live listing first, so edits made since the check are kept).
        /// removeListing: status becomes Removed with the offline reason in the note.
        /// Otherwise only the dead Tor address is taken off and status and note stay as they are.
        /// Returns null on success, else why it couldn't.
        /// </summary>
        private async Task<string?> ApplySiteCheckAsync(Submission submission, bool removeListing, CancellationToken ct)
        {
            if (submission.SubmissionStatus != SubmissionStatus.Pending)
            {
                return $"it is {submission.SubmissionStatus}, not Pending";
            }

            var finding = AutomatedSubmissionRefresh.Classify(submission.NoteToAdmin, submission.DirectoryStatus, submission.Note);
            if (finding is not (SiteCheckFinding.MainSiteOffline or SiteCheckFinding.TorLinkOffline))
            {
                return "it is not an offline finding from the weekly site check";
            }

            if (!removeListing && finding != SiteCheckFinding.TorLinkOffline)
            {
                return "the main site is down, not just the Tor link";
            }

            if (submission.DirectoryEntryId is not int entryId || await this.directoryEntryRepository.GetByIdAsync(entryId) is not DirectoryEntry entry)
            {
                return "the listing no longer exists";
            }

            submission.OriginalSnapshotJson ??= submission.CreateSnapshotJson();
            await this.RefreshAutomatedSnapshotAsync(submission, ct);

            string what;
            if (removeListing)
            {
                submission.DirectoryStatus = DirectoryStatus.Removed;
                what = "removed the listing as offline";
            }
            else
            {
                var onion = OnionLinkOf(entry);
                if (onion == null)
                {
                    return "the listing has no Tor link any more";
                }

                if (UrlHelper.IsOnion(submission.Link2))
                {
                    submission.Link2 = null;
                }

                if (UrlHelper.IsOnion(submission.Link3))
                {
                    submission.Link3 = null;
                }

                submission.DirectoryStatus = entry.DirectoryStatus;
                submission.Note = entry.Note;
                what = $"removed the dead Tor link {onion} and kept the listing";
            }

            var posted = submission.ShallowCopy();
            posted.SubmissionStatus = SubmissionStatus.Approved;
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
            submission.AppendReviewLog($"Admin {this.User?.Identity?.Name}: weekly site check accepted, {what} (listing #{entryId}).");
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
