using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [AllowAnonymous]
    [Route("site/{directoryEntryKey}/admin")]
    public class SiteOwnerAdminController : BaseController
    {
        private const string SessionExpiredMessage =
            "Session expired. Please re-verify with your PGP key.";

        // TempData keys (read by Views/SiteOwnerAdmin/Index.cshtml) used to keep a typed
        // reply when posting fails (moderation rejection or expired session).
        private const string TempDataReplyDraftReviewId = "ReplyDraftReviewId";
        private const string TempDataReplyDraftBody = "ReplyDraftBody";
        private const int MaxReplyDraftLength = 8000;

        private const int AdminSessionMinutes = 12 * 60; // stay signed in
        private const int FlowMinutes = 20;              // short-lived verification flow

        private static readonly char[] CodeAlphabet = StringConstants.CodeAlphabet.ToCharArray();

        private readonly IMemoryCache cache;
        private readonly IPgpService pgp;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly IDirectoryEntryReviewRepository reviewRepo;
        private readonly IDirectoryEntryReviewCommentRepository commentRepo;
        private readonly IUserContentModerationService moderation;

        public SiteOwnerAdminController(
            IMemoryCache cache,
            IPgpService pgp,
            IDirectoryEntryRepository entryRepo,
            IDirectoryEntryReviewRepository reviewRepo,
            IDirectoryEntryReviewCommentRepository commentRepo,
            IUserContentModerationService moderation)
            : base(cache)
        {
            this.cache = cache;
            this.pgp = pgp;
            this.entryRepo = entryRepo;
            this.reviewRepo = reviewRepo;
            this.commentRepo = commentRepo;
            this.moderation = moderation;
        }

        // GET: /site/key/admin
        // GET: /site/key/admin/page/2
        [HttpGet("")]
        [HttpGet("page/{page:int}")]
        public async Task<IActionResult> Index(string directoryEntryKey, int page = 1, CancellationToken ct = default)
        {
            int requestedPage = page < 1 ? 1 : page;

            // /admin/page/1 => /admin (clean URL)
            if (requestedPage == 1 && this.RouteData.Values.ContainsKey("page"))
            {
                return this.RedirectPermanent($"/site/{directoryEntryKey}/admin");
            }

            var entry = await this.entryRepo.GetByKey(directoryEntryKey);
            if (entry == null || entry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return this.NotFound();
            }

            // Not verified (or the listing's PGP key changed since sign-in) => show login (PGP key box)
            if (!this.TryGetSession(entry.DirectoryEntryKey, out var session)
                || !this.SessionStillValidForListing(entry.DirectoryEntryKey, session, entry))
            {
                var loginVm = new SiteOwnerAdminLoginVm
                {
                    DirectoryEntryId = entry.DirectoryEntryId,
                    DirectoryEntryKey = entry.DirectoryEntryKey,
                    DirectoryEntryName = entry.Name,
                    HasPgpKey = !string.IsNullOrWhiteSpace(entry.PgpKey)
                };

                return this.View("~/Views/SiteOwnerAdmin/SubmitKey.cshtml", loginVm);
            }

            // Verified => show admin review list (paged)
            var (vm, effectivePage) = await this.BuildAdminVmAsync(entry, session, requestedPage, ct);

            if (effectivePage != requestedPage)
            {
                return this.RedirectPermanent(AdminPageUrl(entry.DirectoryEntryKey, effectivePage));
            }

            // ✅ THIS WAS YOUR BUG: you were returning SubmitKey with the wrong model
            return this.View("~/Views/SiteOwnerAdmin/Index.cshtml", vm);
        }

        // POST: /site/key/admin/submit-key
        [HttpPost("submit-key")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitKey(
            string directoryEntryKey,
            [FromForm] string? pgpArmored,
            [FromForm] string? website, // honeypot
            CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(website))
            {
                return this.BadRequest();
            }

            var entry = await this.entryRepo.GetByKey(directoryEntryKey);
            if (entry == null || entry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return this.NotFound();
            }

            var loginVm = new SiteOwnerAdminLoginVm
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DirectoryEntryName = entry.Name,
                HasPgpKey = !string.IsNullOrWhiteSpace(entry.PgpKey)
            };

            if (string.IsNullOrWhiteSpace(entry.PgpKey))
            {
                this.ModelState.AddModelError(string.Empty, "This listing does not have a PGP key set, so owner admin cannot be verified.");
                return this.View("~/Views/SiteOwnerAdmin/SubmitKey.cshtml", loginVm);
            }

            pgpArmored = (pgpArmored ?? string.Empty).Trim();
            var fp = this.pgp.GetFingerprint(pgpArmored);

            if (string.IsNullOrWhiteSpace(fp))
            {
                this.ModelState.AddModelError(string.Empty, "Invalid PGP public key.");
                return this.View("~/Views/SiteOwnerAdmin/SubmitKey.cshtml", loginVm);
            }

            // ✅ Must match the listing’s stored PGP key. Historically the submitted identity
            // fingerprint was matched against ANY stored fingerprint (primary or subkey). We ALSO
            // accept a match on the PRIMARY key fingerprint, so a routine OpenPGP key update — adding
            // or rotating an encryption subkey, extending expiry — is still recognized as the same
            // owner instead of being rejected as a different key. The primary fingerprint is
            // unforgeable (an attacker would have to reuse the owner's real primary public key, whose
            // secret they don't hold), and the encryption challenge below still proves control by
            // requiring the code to be decrypted with the key's (verified) encryption subkey.
            var entryFps = PgpFingerprintTools.GetAllFingerprints(entry.PgpKey);
            var submittedNorm = PgpFingerprintTools.Normalize(fp);

            bool matchesAnyStoredFp = entryFps.Any(listingFp => PgpFingerprintTools.Matches(submittedNorm, listingFp));

            var storedPrimaryFp = PgpCapabilities.GetPrimaryFingerprint(entry.PgpKey);
            var submittedPrimaryFp = PgpCapabilities.GetPrimaryFingerprint(pgpArmored);
            bool matchesPrimaryFp =
                !string.IsNullOrEmpty(storedPrimaryFp) &&
                !string.IsNullOrEmpty(submittedPrimaryFp) &&
                PgpFingerprintTools.Matches(storedPrimaryFp, submittedPrimaryFp);

            bool matchesListingKey = matchesAnyStoredFp || matchesPrimaryFp;
            if (!matchesListingKey)
            {
                this.ModelState.AddModelError(string.Empty, "That PGP key does not match the PGP key on this listing.");
                return this.View("~/Views/SiteOwnerAdmin/SubmitKey.cshtml", loginVm);
            }

            // Create a short-lived flow
            var flowId = Guid.NewGuid();

            var expectedNormalized = GenerateChallengeCodeNormalized(IntegerConstants.ChallengeLength);
            var plaintextForUser = FormatChallengeCodeForHumans(expectedNormalized);

            // Ownership is proven by decrypting a one-time challenge, so the key MUST have a usable
            // encryption (sub)key. Sign-only keys, and keys whose only encryption subkey has expired or
            // been revoked, cannot be encrypted to — PgpService.EncryptTo throws for those. Catch it and
            // show a clear message instead of a 500.
            string cipher;
            try
            {
                cipher = this.pgp.EncryptTo(pgpArmored, plaintextForUser);
            }
            catch (Exception)
            {
                this.ModelState.AddModelError(
                    string.Empty,
                    "This PGP key can't be used to verify ownership because it has no usable encryption key: " +
                    "it is either sign-only, or its encryption subkey has expired or been revoked. " +
                    "Ownership is verified by encrypting a one-time code to your key, which needs a current, " +
                    "non-expired encryption subkey. Please update the listing's PGP key to one with a valid " +
                    "encryption subkey and try again.");
                return this.View("~/Views/SiteOwnerAdmin/SubmitKey.cshtml", loginVm);
            }

            if (string.IsNullOrWhiteSpace(cipher))
            {
                this.ModelState.AddModelError(string.Empty, "Could not encrypt the challenge to that key. Please try again with a valid armored public key.");
                return this.View("~/Views/SiteOwnerAdmin/SubmitKey.cshtml", loginVm);
            }

            var flow = new SiteOwnerAdminFlowState
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DirectoryEntryName = entry.Name,

                PgpArmored = pgpArmored,
                PgpFingerprint = fp,

                ChallengeCode = expectedNormalized,
                ChallengeCiphertext = cipher,
                VerifyAttempts = 0,

                ExpiresUtc = DateTime.UtcNow.AddMinutes(FlowMinutes)
            };

            this.cache.Set(FlowCacheKey(flowId), flow, flow.ExpiresUtc);

            return this.RedirectToAction(nameof(this.Verify), new { directoryEntryKey = entry.DirectoryEntryKey, flowId });
        }

        // GET: /site/key/admin/verify?flowId=...
        [HttpGet("verify")]
        public IActionResult Verify(string directoryEntryKey, Guid flowId)
        {
            if (!this.TryGetFlow(flowId, out var flow) ||
                !string.Equals(flow.DirectoryEntryKey, directoryEntryKey, StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            this.ViewBag.FlowId = flowId;
            this.ViewBag.Ciphertext = flow.ChallengeCiphertext;

            // used by the shared partial
            this.ViewBag.PostController = "SiteOwnerAdmin";
            this.ViewBag.PostAction = "VerifyPost";
            this.ViewBag.DirectoryEntryKey = directoryEntryKey;

            return this.View("~/Views/SiteOwnerAdmin/VerifyCode.cshtml");
        }

        // POST: /site/key/admin/verify
        [HttpPost("verify")]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyPost(string directoryEntryKey, Guid flowId, string? code)
        {
            if (!this.TryGetFlow(flowId, out var flow) ||
                !string.Equals(flow.DirectoryEntryKey, directoryEntryKey, StringComparison.OrdinalIgnoreCase))
            {
                return this.BadRequest(SessionExpiredMessage);
            }

            flow.VerifyAttempts++;
            if (flow.VerifyAttempts > IntegerConstants.MaxVerifyAttempts)
            {
                this.cache.Remove(FlowCacheKey(flowId));
                return this.BadRequest("Too many attempts. Please start over.");
            }

            var submitted = NormalizeSubmittedCode(code);

            if (!CodesMatchConstantTime(submitted, flow.ChallengeCode ?? string.Empty))
            {
                this.ModelState.AddModelError(
                    string.Empty,
                    "That code doesn’t match. Decrypt the message again and enter the code exactly as shown (dashes/spaces don’t matter).");

                // re-render verify view
                this.ViewBag.FlowId = flowId;
                this.ViewBag.Ciphertext = flow.ChallengeCiphertext;
                this.ViewBag.PostController = "SiteOwnerAdmin";
                this.ViewBag.PostAction = "VerifyPost";
                this.ViewBag.DirectoryEntryKey = directoryEntryKey;

                this.cache.Set(FlowCacheKey(flowId), flow, flow.ExpiresUtc);
                return this.View("~/Views/SiteOwnerAdmin/VerifyCode.cshtml");
            }

            // ✅ Create admin session (cookie + cache)
            var sessionId = Guid.NewGuid();
            var session = new SiteOwnerAdminSessionState
            {
                DirectoryEntryId = flow.DirectoryEntryId,
                DirectoryEntryKey = flow.DirectoryEntryKey,
                DirectoryEntryName = flow.DirectoryEntryName,
                OwnerFingerprint = flow.PgpFingerprint ?? string.Empty,
                OwnerPrimaryFingerprint = PgpCapabilities.GetPrimaryFingerprint(flow.PgpArmored ?? string.Empty) ?? string.Empty,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(AdminSessionMinutes)
            };

            this.cache.Set(SessionCacheKey(sessionId), session, session.ExpiresUtc);
            this.SetSessionCookie(directoryEntryKey, sessionId, session.ExpiresUtc);

            this.cache.Remove(FlowCacheKey(flowId));

            this.TempData["AdminMessage"] = "Verified. You’re signed in to the owner admin panel.";
            return this.Redirect(AdminPageUrl(directoryEntryKey, page: 1));
        }

        // POST: /site/key/admin/reply
        [HttpPost("reply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(
            string directoryEntryKey,
            [FromForm] int directoryEntryReviewId,
            [FromForm] string? body,
            [FromForm] int returnPage = 1,
            [FromForm] int? parentCommentId = null,
            CancellationToken ct = default)
        {
            if (!this.TryGetSession(directoryEntryKey, out var session))
            {
                this.TempData["AdminError"] = SessionExpiredMessage;
                this.SaveReplyDraft(directoryEntryReviewId, body);
                return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
            }

            var entry = await this.entryRepo.GetByKey(directoryEntryKey);
            if (entry == null || entry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return this.NotFound();
            }

            if (!this.SessionStillValidForListing(directoryEntryKey, session, entry))
            {
                this.TempData["AdminError"] = SessionExpiredMessage;
                this.SaveReplyDraft(directoryEntryReviewId, body);
                return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
            }

            var review = await this.reviewRepo.GetByIdAsync(directoryEntryReviewId, ct);
            if (review == null || review.DirectoryEntryId != entry.DirectoryEntryId)
            {
                return this.NotFound();
            }

            // A nested reply must point at an existing, non-rejected reply on THIS review —
            // never at a comment on another review/listing (the posted id is untrusted).
            if (parentCommentId.HasValue)
            {
                var parent = parentCommentId.Value > 0
                    ? await this.commentRepo.GetByIdAsync(parentCommentId.Value, ct)
                    : null;

                if (!IsValidParentComment(parent, directoryEntryReviewId))
                {
                    this.TempData["AdminError"] = "The reply you were responding to no longer exists.";
                    this.SaveReplyDraft(directoryEntryReviewId, body);
                    return this.Redirect(AdminPageUrl(directoryEntryKey, returnPage) + $"#review-{directoryEntryReviewId}");
                }
            }

            body ??= string.Empty;
            var typedBody = body;

            // Emojis are not allowed in replies/comments — strip any out before moderation and saving.
            body = DirectoryManager.Utilities.Validation.UnicodeSanitizer.StripEmoji(body);

            var mod = await this.moderation.EvaluateReplyAsync(body, ct);
            if (!mod.IsValid)
            {
                this.TempData["AdminError"] = mod.ValidationErrorMessage ?? "Invalid reply.";
                this.SaveReplyDraft(directoryEntryReviewId, typedBody);
                return this.Redirect(AdminPageUrl(directoryEntryKey, returnPage) + $"#review-{directoryEntryReviewId}");
            }

            // The Body column holds 4,000 characters: reject longer replies (keeping the draft)
            // instead of letting the insert fail with a 500.
            if (body.Trim().Length > 4000)
            {
                this.TempData["AdminError"] = "Your reply is too long. Please keep it under 4,000 characters.";
                this.SaveReplyDraft(directoryEntryReviewId, typedBody);
                return this.Redirect(AdminPageUrl(directoryEntryKey, returnPage) + $"#review-{directoryEntryReviewId}");
            }

            var entity = new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = directoryEntryReviewId,
                ParentCommentId = parentCommentId,
                Body = body.Trim(),

                // owner replies can be immediately visible
                ModerationStatus = ReviewModerationStatus.Approved,

                // The listing owner is replying — flag it so notifications skip their own replies.
                IsOwner = true,

                AuthorFingerprint = session.OwnerFingerprint,
                CreateDate = DateTime.UtcNow,
                CreatedByUserId = "site-owner-admin"
            };

            await this.commentRepo.AddAsync(entity, ct);

            this.ClearCachedItems();
            this.TempData["AdminMessage"] = "Reply posted.";

            return this.Redirect(AdminPageUrl(directoryEntryKey, returnPage) + $"#review-{directoryEntryReviewId}");
        }

        // POST: /site/key/admin/notifications  — toggle review/reply email notifications
        [HttpPost("notifications")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNotifications(
            string directoryEntryKey,
            [FromForm] bool enabled,
            CancellationToken ct = default)
        {
            if (!this.TryGetSession(directoryEntryKey, out var session))
            {
                this.TempData["AdminError"] = SessionExpiredMessage;
                return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
            }

            var entry = await this.entryRepo.GetByKey(directoryEntryKey);
            if (entry == null || entry.DirectoryStatus == DirectoryStatus.Removed)
            {
                return this.NotFound();
            }

            if (!this.SessionStillValidForListing(directoryEntryKey, session, entry))
            {
                this.TempData["AdminError"] = SessionExpiredMessage;
                return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
            }

            // Can only enable when the listing has a real, well-formed email to send to.
            if (enabled && !DirectoryManager.Utilities.Helpers.EmailValidationHelper.Validate(entry.Email).IsValid)
            {
                this.TempData["AdminError"] = "Add a valid email address to your listing before enabling review notifications.";
                return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
            }

            // Stamp the enabled-at watermark the first time it's turned on, so only reviews/replies
            // submitted after this point ever trigger an email (no backlog spam).
            if (enabled && !entry.ReviewEmailNotificationsEnabled)
            {
                entry.ReviewEmailNotificationsEnabledUtc = DateTime.UtcNow;
            }

            entry.ReviewEmailNotificationsEnabled = enabled;
            await this.entryRepo.UpdateAsync(entry);
            this.ClearCachedItems();

            this.TempData["AdminMessage"] = enabled
                ? "Email notifications enabled — we'll email you when a new review or reply goes live."
                : "Email notifications disabled.";
            return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
        }

        // POST: /site/key/admin/logout
        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout(string directoryEntryKey)
        {
            if (this.TryGetSessionIdFromCookie(directoryEntryKey, out var sid))
            {
                this.cache.Remove(SessionCacheKey(sid));
            }

            this.ClearSessionCookie(directoryEntryKey);

            this.TempData["AdminMessage"] = "Signed out.";
            return this.Redirect(AdminPageUrl(directoryEntryKey, 1));
        }

        /// <summary>
        /// Same matching rule as sign-in (SubmitKey): the session's identity fingerprint matches
        /// any fingerprint of the listing's current key, or the session's primary-key fingerprint
        /// matches the current key's primary fingerprint. No key on the listing = no session.
        /// </summary>
        internal static bool SessionKeyMatchesListingKey(
            string? sessionFingerprint, string? sessionPrimaryFingerprint, string? listingPgpKey)
        {
            if (string.IsNullOrWhiteSpace(listingPgpKey) || string.IsNullOrWhiteSpace(sessionFingerprint))
            {
                return false;
            }

            var sessionNorm = PgpFingerprintTools.Normalize(sessionFingerprint);
            if (PgpFingerprintTools.GetAllFingerprints(listingPgpKey)
                .Any(listingFp => PgpFingerprintTools.Matches(sessionNorm, listingFp)))
            {
                return true;
            }

            var listingPrimary = PgpCapabilities.GetPrimaryFingerprint(listingPgpKey);
            return !string.IsNullOrEmpty(listingPrimary)
                && !string.IsNullOrEmpty(sessionPrimaryFingerprint)
                && PgpFingerprintTools.Matches(listingPrimary, sessionPrimaryFingerprint);
        }

        /// <summary>A posted parent reply is acceptable only if it exists on the same review and isn't rejected.</summary>
        internal static bool IsValidParentComment(DirectoryEntryReviewComment? parent, int directoryEntryReviewId) =>
            parent != null
            && parent.DirectoryEntryReviewId == directoryEntryReviewId
            && parent.ModerationStatus != ReviewModerationStatus.Rejected;

        // =========================
        // VM builder
        // =========================
        private async Task<(SiteOwnerAdminIndexVm Vm, int EffectivePage)> BuildAdminVmAsync(
            DirectoryEntry entry,
            SiteOwnerAdminSessionState session,
            int requestedPage,
            CancellationToken ct)
        {
            int pageSize = IntegerConstants.ReviewsPageSize;

            // Admin: show all reviews except Removed
            var q = this.reviewRepo.Query()
                .Where(r => r.DirectoryEntryId == entry.DirectoryEntryId &&
                            r.ModerationStatus != ReviewModerationStatus.Rejected)
                .OrderByDescending(r => r.UpdateDate ?? r.CreateDate)
                .ThenByDescending(r => r.DirectoryEntryReviewId);

            int total = await q.CountAsync(ct);

            int totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages < 1)
            {
                totalPages = 1;
            }

            int page = requestedPage;
            if (page < 1)
            {
                page = 1;
            }

            if (page > totalPages)
            {
                page = totalPages;
            }

            var reviews = await q.Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync(ct);

            var reviewIds = reviews.Select(r => r.DirectoryEntryReviewId).ToList();

            var replies = (reviewIds.Count == 0)
                ? new List<DirectoryEntryReviewComment>()
                : await this.commentRepo.Query()
                    .Where(c => reviewIds.Contains(c.DirectoryEntryReviewId) &&
                                c.ModerationStatus != ReviewModerationStatus.Rejected)
                    .OrderBy(c => c.CreateDate)
                    .ThenBy(c => c.DirectoryEntryReviewCommentId)
                    .ToListAsync(ct);

            ApplyOwnerNames(entry, reviews, replies);

            var lookup = replies
                .GroupBy(x => x.DirectoryEntryReviewId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var vm = new SiteOwnerAdminIndexVm
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                DirectoryEntryName = entry.Name,

                OwnerFingerprint = session.OwnerFingerprint,
                SessionExpiresUtc = session.ExpiresUtc,

                Reviews = reviews,
                RepliesByReviewId = lookup,

                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,

                NotificationsEnabled = entry.ReviewEmailNotificationsEnabled,
                ProfileEmail = entry.Email,
                EmailIsValid = DirectoryManager.Utilities.Helpers.EmailValidationHelper.Validate(entry.Email).IsValid
            };

            return (vm, page);
        }

        private static void ApplyOwnerNames(
            DirectoryEntry entry,
            List<DirectoryEntryReview> reviews,
            List<DirectoryEntryReviewComment> replies)
        {
            if (string.IsNullOrWhiteSpace(entry.PgpKey))
            {
                return;
            }

            var entryFps = PgpFingerprintTools.GetAllFingerprints(entry.PgpKey);

            foreach (var r in reviews)
            {
                var reviewNorm = PgpFingerprintTools.Normalize(r.AuthorFingerprint);
                bool isOwner = entryFps.Any(fp => PgpFingerprintTools.Matches(reviewNorm, fp));
                r.DisplayName = isOwner ? entry.Name : null;
            }

            foreach (var c in replies)
            {
                var replyNorm = PgpFingerprintTools.Normalize(c.AuthorFingerprint);
                bool isOwner = entryFps.Any(fp => PgpFingerprintTools.Matches(replyNorm, fp));
                c.DisplayName = isOwner ? entry.Name : null;
            }
        }

        // =========================
        // Session + Flow helpers
        // =========================
        private static string AdminPageUrl(string key, int page)
            => page <= 1 ? $"/site/{key}/admin" : $"/site/{key}/admin/page/{page}";

        private static string CookieName(string key) => $"site_owner_admin_{key}";
        private static string FlowCacheKey(Guid flowId) => $"site-owner-admin-flow:{flowId}";
        private static string SessionCacheKey(Guid sessionId) => $"site-owner-admin-session:{sessionId}";

        private void SetSessionCookie(string directoryEntryKey, Guid sessionId, DateTime expiresUtc)
        {
            this.Response.Cookies.Append(
                CookieName(directoryEntryKey),
                sessionId.ToString("N"),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = this.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = expiresUtc,
                    IsEssential = true
                });
        }

        // Keeps the owner's typed reply (as plain text, HTML-encoded on render) so a failed
        // post doesn't lose it. Only stored, never saved/approved — moderation still applies on resubmit.
        private void SaveReplyDraft(int directoryEntryReviewId, string? body)
        {
            if (directoryEntryReviewId <= 0 || string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            var draft = body.Length > MaxReplyDraftLength ? body.Substring(0, MaxReplyDraftLength) : body;
            this.TempData[TempDataReplyDraftReviewId] = directoryEntryReviewId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            this.TempData[TempDataReplyDraftBody] = draft;
        }

        private void ClearSessionCookie(string directoryEntryKey)
        {
            this.Response.Cookies.Delete(CookieName(directoryEntryKey));
        }

        private bool TryGetSession(string directoryEntryKey, out SiteOwnerAdminSessionState session)
        {
            session = default!;

            if (!this.TryGetSessionIdFromCookie(directoryEntryKey, out var sid))
            {
                return false;
            }

            if (!this.cache.TryGetValue(SessionCacheKey(sid), out session))
            {
                return false;
            }

            if (session.ExpiresUtc <= DateTime.UtcNow)
            {
                this.cache.Remove(SessionCacheKey(sid));
                return false;
            }

            if (!string.Equals(session.DirectoryEntryKey, directoryEntryKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        // The owner session is bound to the PGP key that was on the listing at sign-in. Re-check it
        // against the listing's CURRENT key on every request: if an admin changed/removed the key
        // (e.g. ownership moved), the old session is dropped instead of living on for up to 12h.
        private bool SessionStillValidForListing(
            string directoryEntryKey, SiteOwnerAdminSessionState session, DirectoryEntry entry)
        {
            if (session.DirectoryEntryId == entry.DirectoryEntryId
                && SessionKeyMatchesListingKey(session.OwnerFingerprint, session.OwnerPrimaryFingerprint, entry.PgpKey))
            {
                return true;
            }

            if (this.TryGetSessionIdFromCookie(directoryEntryKey, out var sid))
            {
                this.cache.Remove(SessionCacheKey(sid));
            }

            this.ClearSessionCookie(directoryEntryKey);
            return false;
        }

        private bool TryGetSessionIdFromCookie(string directoryEntryKey, out Guid sessionId)
        {
            sessionId = default;

            if (!this.Request.Cookies.TryGetValue(CookieName(directoryEntryKey), out var raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return Guid.TryParseExact(raw.Trim(), "N", out sessionId);
        }

        private bool TryGetFlow(Guid flowId, out SiteOwnerAdminFlowState flow)
        {
            if (!this.cache.TryGetValue(FlowCacheKey(flowId), out flow))
            {
                return false;
            }

            if (flow.ExpiresUtc <= DateTime.UtcNow)
            {
                this.cache.Remove(FlowCacheKey(flowId));
                return false;
            }

            return true;
        }

        // =========================
        // Challenge helpers
        // =========================
        private static string GenerateChallengeCodeNormalized(int length)
        {
            if (length < 6)
            {
                length = 6;
            }

            Span<char> chars = stackalloc char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            return new string(chars);
        }

        private static string FormatChallengeCodeForHumans(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length <= 5)
            {
                return normalized;
            }

            return normalized.Substring(0, 5) + "-" + normalized.Substring(5);
        }

        private static string NormalizeSubmittedCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);
            foreach (var ch in input.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static bool CodesMatchConstantTime(string submitted, string expected)
        {
            if (string.IsNullOrEmpty(submitted) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            if (submitted.Length != expected.Length)
            {
                return false;
            }

            var a = Encoding.UTF8.GetBytes(submitted);
            var b = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        // =========================
        // View models
        // =========================
        public sealed class SiteOwnerAdminLoginVm
        {
            public int DirectoryEntryId { get; set; }
            public string DirectoryEntryKey { get; set; } = string.Empty;
            public string DirectoryEntryName { get; set; } = string.Empty;
            public bool HasPgpKey { get; set; }
        }

        public sealed class SiteOwnerAdminIndexVm
        {
            public int DirectoryEntryId { get; set; }
            public string DirectoryEntryKey { get; set; } = string.Empty;
            public string DirectoryEntryName { get; set; } = string.Empty;

            public string OwnerFingerprint { get; set; } = string.Empty;
            public DateTime SessionExpiresUtc { get; set; }

            public List<DirectoryEntryReview> Reviews { get; set; } = new ();
            public Dictionary<int, List<DirectoryEntryReviewComment>> RepliesByReviewId { get; set; } = new ();

            public int CurrentPage { get; set; }
            public int TotalPages { get; set; }
            public int PageSize { get; set; }

            // Review/reply email-notification settings.
            public bool NotificationsEnabled { get; set; }
            public string? ProfileEmail { get; set; }
            public bool EmailIsValid { get; set; }
        }

        private sealed class SiteOwnerAdminFlowState
        {
            public int DirectoryEntryId { get; set; }
            public string DirectoryEntryKey { get; set; } = string.Empty;
            public string DirectoryEntryName { get; set; } = string.Empty;

            public string? PgpArmored { get; set; }
            public string? PgpFingerprint { get; set; }

            public string? ChallengeCode { get; set; }
            public string? ChallengeCiphertext { get; set; }
            public int VerifyAttempts { get; set; }

            public DateTime ExpiresUtc { get; set; }
        }

        private sealed class SiteOwnerAdminSessionState
        {
            public int DirectoryEntryId { get; set; }
            public string DirectoryEntryKey { get; set; } = string.Empty;
            public string DirectoryEntryName { get; set; } = string.Empty;

            public string OwnerFingerprint { get; set; } = string.Empty;

            // Primary-key fingerprint of the key the owner signed in with (see SubmitKey), so a
            // routine subkey rotation on the listing doesn't log them out.
            public string OwnerPrimaryFingerprint { get; set; } = string.Empty;
            public DateTime ExpiresUtc { get; set; }
        }
    }
}