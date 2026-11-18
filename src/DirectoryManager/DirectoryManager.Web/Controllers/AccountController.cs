using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class AccountController : BaseController
    {
        public const string FailedLoginMessage =
            "Invalid login attempt. After several failed attempts, login is paused for a few minutes.";

        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISubmissionRepository submissionRepository;
        private readonly IDirectoryEntryReviewRepository directoryEntryReviewRepository;
        private readonly IDirectoryEntryReviewCommentRepository directoryEntryReviewCommentRepository;
        private readonly IVerificationRequestRepository verificationRequestRepository;
        private readonly IProblemReportRepository problemReportRepository;
        private readonly IVideoSponsorshipRepository videoSponsorshipRepository;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ICaptchaService captchaService;
        private readonly AdminLoginService adminLoginService;
        private readonly IStaticPublishRepository staticPublishRepository;
        private readonly Microsoft.Extensions.Options.IOptionsMonitor<DirectoryManager.Web.Models.StaticPublishing.StaticPublishOptions> staticPublishOptions;
        public readonly ISponsoredListingInvoiceRepository SponsoredListingInvoiceRepository;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository directoryEntryRepository,
            ISubmissionRepository submissionRepository,
            IDirectoryEntryReviewRepository directoryEntryReviewRepository,
            IDirectoryEntryReviewCommentRepository directoryEntryReviewCommentRepository,
            IVerificationRequestRepository verificationRequestRepository,
            IProblemReportRepository problemReportRepository,
            IVideoSponsorshipRepository videoSponsorshipRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ICaptchaService captchaService,
            IMemoryCache cache,
            IStaticPublishRepository staticPublishRepository,
            Microsoft.Extensions.Options.IOptionsMonitor<DirectoryManager.Web.Models.StaticPublishing.StaticPublishOptions> staticPublishOptions,
            AdminLoginService adminLoginService)
            : base(cache)
        {
            this.adminLoginService = adminLoginService;
            this.staticPublishRepository = staticPublishRepository;
            this.staticPublishOptions = staticPublishOptions;
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.directoryEntryRepository = directoryEntryRepository;
            this.submissionRepository = submissionRepository;
            this.directoryEntryReviewRepository = directoryEntryReviewRepository;
            this.verificationRequestRepository = verificationRequestRepository;
            this.problemReportRepository = problemReportRepository;
            this.videoSponsorshipRepository = videoSponsorshipRepository;
            this.directoryEntryReviewCommentRepository = directoryEntryReviewCommentRepository;
            this.SponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.captchaService = captchaService;
        }

        [HttpGet]
        [Route("account/login")]
        public IActionResult Login()
        {
            return this.View();
        }

        [Route("account/login")]
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe, string? twoFactorCode = null)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                this.ModelState.AddModelError("", "Username and password are required.");
                return this.View();
            }

            // 🔒 CAPTCHA — bot/credential-stuffing brake
            if (!this.captchaService.IsValid(this.Request))
            {
                this.ModelState.AddModelError("", "Invalid CAPTCHA. Please try again.");
                return this.View();
            }

            // 🔒 Password (+ authenticator/recovery code when 2FA is on) with Identity's failed-attempt
            // counter. With 2FA on, password + valid code gets in even during a lockout (see AdminLoginService).
            var result = await this.adminLoginService.CheckAsync(username, password, twoFactorCode);

            if (result.Outcome == AdminLoginOutcome.Success && result.User != null)
            {
                await this.signInManager.SignInWithClaimsAsync(
                    result.User,
                    rememberMe,
                    new[] { new System.Security.Claims.Claim("amr", AdminLoginService.AuthenticationMethod(result)) });
                return this.RedirectToAction("Home", "Account");
            }

            if (result.Outcome == AdminLoginOutcome.NotAllowed)
            {
                this.ModelState.AddModelError("", "Login not allowed.");
                return this.View();
            }

            // One message for every failure (unknown user, wrong password, missing/wrong code, locked out),
            // so a stranger can't learn that the username exists or that the account is locked.
            this.ModelState.AddModelError("", FailedLoginMessage);
            return this.View();
        }

        [Route("account/home")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public async Task<IActionResult> HomeAsync()
        {
            var totalPendingSubmissions =
                await this.submissionRepository.GetByStatus(Data.Enums.SubmissionStatus.Pending);

            // Pending reviews
            var pendingReviews = await this.directoryEntryReviewRepository
                .Query()
                .Where(r => (r.ModerationStatus == ReviewModerationStatus.Pending || r.ModerationStatus == ReviewModerationStatus.Flagged))
                .CountAsync();

            // Pending replies/comments
            var pendingReviewComments = await this.directoryEntryReviewCommentRepository
                .Query()
                .Where(c => (c.ModerationStatus == ReviewModerationStatus.Pending || c.ModerationStatus == ReviewModerationStatus.Flagged))
                .CountAsync();

            // Combined total for your dashboard badge
            var totalPendingReviewItems = pendingReviews + pendingReviewComments;

            var pendingVerificationRequests =
                await this.verificationRequestRepository.CountByStatusAsync(Data.Enums.VerificationRequestStatus.Pending);

            this.ViewBag.TotalPendingSubmissions = totalPendingSubmissions;

            // Automated reviewer: how many pending submissions are ready to approve vs need a person.
            var reviewStates = (await this.submissionRepository.ListPendingForReviewAsync())
                .Select(x => x.ReviewState)
                .ToList();
            this.ViewBag.AiReadyToApprove = reviewStates.Count(x => x == Data.Enums.SubmissionReviewState.AiApproveCandidate);
            this.ViewBag.AiNeedsAttention = reviewStates.Count(x => x is Data.Enums.SubmissionReviewState.Flagged
                or Data.Enums.SubmissionReviewState.AiRejectCandidate
                or Data.Enums.SubmissionReviewState.AutoRejected
                or Data.Enums.SubmissionReviewState.Skipped);
            this.ViewBag.AiNotReviewedYet = reviewStates.Count(x => x == Data.Enums.SubmissionReviewState.Pending);
            this.ViewBag.LastSubmissionUtc = await this.submissionRepository.GetLastSubmissionUtcAsync();
            this.ViewBag.LastSubmissionPaidUtc = await this.submissionRepository.GetLastPaidUtcAsync();
            this.ViewBag.TotalPendingReviews = totalPendingReviewItems;
            this.ViewBag.LastInvoicePaidUtc = this.SponsoredListingInvoiceRepository.GetLastPaidInvoiceCreateDate();
            this.ViewBag.LastInvoicePendingUtc = this.SponsoredListingInvoiceRepository.GetLastPendingInvoiceCreateDate();
            this.ViewBag.InvoiceAttention = this.SponsoredListingInvoiceRepository.GetPaymentAttentionCounts();
            this.ViewBag.RevenueSummary = await this.HttpContext.RequestServices
                .GetRequiredService<DirectoryManager.Web.Services.Implementations.RevenueOpportunityService>()
                .GetSummaryAsync();
            this.ViewBag.TotalPendingReviewReviews = pendingReviews;

            // Newest review of any status, so auto-approved ones don't go unnoticed.
            this.ViewBag.LastReview = await this.directoryEntryReviewRepository
                .Query()
                .AsNoTracking()
                .OrderByDescending(r => r.CreateDate)
                .Select(r => new DirectoryManager.Web.Models.LastReviewInfo
                {
                    ReviewId = r.DirectoryEntryReviewId,
                    ListingName = r.DirectoryEntry.Name,
                    ListingKey = r.DirectoryEntry.DirectoryEntryKey,
                    Rating = r.Rating,
                    CreatedUtc = r.CreateDate,
                    Status = r.ModerationStatus,
                    AutoResult = r.AutoModerationResult,
                })
                .FirstOrDefaultAsync();
            this.ViewBag.TotalPendingReviewComments = pendingReviewComments;
            this.ViewBag.TotalPendingVerificationRequests = pendingVerificationRequests;
            this.ViewBag.LastVerificationSubmissionUtc = await this.verificationRequestRepository.GetLastSubmissionUtcAsync();
            this.ViewBag.LastVerificationPaidUtc = await this.verificationRequestRepository.GetLastPaidUtcAsync();

            this.ViewBag.TotalPendingProblemReports =
                await this.problemReportRepository.CountByStatusAsync(Data.Enums.ProblemReportStatus.Pending);
            this.ViewBag.LastProblemReportSubmissionUtc = await this.problemReportRepository.GetLastSubmissionUtcAsync();
            this.ViewBag.LastProblemReportPaidUtc = await this.problemReportRepository.GetLastPaidUtcAsync();

            this.ViewBag.LastVideoSponsorPaidUtc = await this.videoSponsorshipRepository.GetLastPaidUtcAsync();

            // Last payment per income source (Last paid column: when + USD + XMR), and the single most
            // recent payment of any kind (shown above Daily Tasks, linked to the item).
            var sub = await this.submissionRepository.GetLastPaymentAsync();
            var prob = await this.problemReportRepository.GetLastPaymentAsync();
            var verif = await this.verificationRequestRepository.GetLastPaymentAsync();
            var vid = await this.videoSponsorshipRepository.GetLastPaymentAsync();
            var inv = this.SponsoredListingInvoiceRepository.GetLastPaidInvoice();
            var invUsd = inv.Currency is DirectoryManager.Data.Enums.Currency.USD or DirectoryManager.Data.Enums.Currency.Unknown ? inv.Amount : null;
            this.ViewBag.PaySubmission = (sub.PaidUtc, sub.Usd, sub.Xmr);
            this.ViewBag.PayProblem = (prob.PaidUtc, prob.Usd, prob.Xmr);
            this.ViewBag.PayVerification = (verif.PaidUtc, verif.Usd, verif.Xmr);
            this.ViewBag.PayVideo = (vid.PaidUtc, vid.Usd, vid.Xmr);
            this.ViewBag.PayInvoice = (inv.PaidUtc, invUsd, inv.Xmr);
            this.ViewBag.LastPayment = new (DateTime? When, decimal? Usd, decimal? Xmr, string What, string Url)[]
                {
                    (sub.PaidUtc, sub.Usd, sub.Xmr, $"Listing submission #{sub.Id}", $"/submission/{sub.Id}"),
                    (prob.PaidUtc, prob.Usd, prob.Xmr, $"Problem report #{prob.Id}", $"/admin/problem-reports/{prob.Id}"),
                    (verif.PaidUtc, verif.Usd, verif.Xmr, $"Verification request #{verif.Id}", $"/admin/verification-requests/{verif.Id}"),
                    (vid.PaidUtc, vid.Usd, vid.Xmr, $"Video sponsorship order #{vid.Id}", $"/admin/video-sponsorship/orders/{vid.Id}"),
                    (inv.PaidUtc, invUsd, inv.Xmr, $"Site sponsorship invoice #{inv.Id}", $"/sponsoredlistinginvoice/details?id={inv.Id}"),
                }
                .Where(x => x.When.HasValue)
                .OrderByDescending(x => x.When)
                .Select(x => ((DateTime, decimal?, decimal?, string, string)?)(x.When!.Value, x.Usd, x.Xmr, x.What, x.Url))
                .FirstOrDefault();
            // Paid and still waiting: every donation-backed item nobody has handled yet, newest payment first.
            this.ViewBag.PaidWaiting =
                (await this.submissionRepository.ListPaidPendingAsync())
                    .Select(x => (Item: x, What: "Listing submission", Url: $"/submission/{x.Id}"))
                .Concat((await this.verificationRequestRepository.ListPaidPendingAsync())
                    .Select(x => (Item: x, What: "Verification request", Url: $"/admin/verification-requests/{x.Id}")))
                .Concat((await this.problemReportRepository.ListPaidPendingAsync())
                    .Select(x => (Item: x, What: "Problem report", Url: $"/admin/problem-reports/{x.Id}")))
                .OrderByDescending(x => x.Item.PaidUtc)
                .ToList();
            // The most recent paid item of each kind, whatever became of it, so the page can say
            // how it ended (submission, verification request and problem report alike).
            var lastPaidItems = new List<(string What, int Id, string Name, string Url, DateTime PaidUtc, decimal? Usd, decimal? Xmr, bool Waiting, string Status)>();
            var lastPaidSubmission = sub.Id > 0 ? await this.submissionRepository.GetByIdAsync(sub.Id) : null;
            if (lastPaidSubmission?.PaidUtc is DateTime subPaid)
            {
                lastPaidItems.Add(("submission", lastPaidSubmission.SubmissionId, lastPaidSubmission.Name, $"/submission/{lastPaidSubmission.SubmissionId}", subPaid, lastPaidSubmission.PaidUsd, lastPaidSubmission.PaidXmr, lastPaidSubmission.SubmissionStatus == Data.Enums.SubmissionStatus.Pending, lastPaidSubmission.SubmissionStatus.ToString()));
            }

            var lastPaidVerification = verif.Id > 0 ? await this.verificationRequestRepository.GetByIdAsync(verif.Id) : null;
            if (lastPaidVerification?.PaidUtc is DateTime verifPaid)
            {
                var listing = await this.directoryEntryRepository.GetByIdAsync(lastPaidVerification.DirectoryEntryId);
                lastPaidItems.Add(("verification request", lastPaidVerification.VerificationRequestId, listing?.Name ?? string.Empty, $"/admin/verification-requests/{lastPaidVerification.VerificationRequestId}", verifPaid, lastPaidVerification.PaidUsd, lastPaidVerification.PaidXmr, lastPaidVerification.Status == Data.Enums.VerificationRequestStatus.Pending, lastPaidVerification.Status.ToString()));
            }

            var lastPaidProblem = prob.Id > 0 ? await this.problemReportRepository.GetByIdAsync(prob.Id) : null;
            if (lastPaidProblem?.PaidUtc is DateTime probPaid)
            {
                var listing = await this.directoryEntryRepository.GetByIdAsync(lastPaidProblem.DirectoryEntryId);
                lastPaidItems.Add(("problem report", lastPaidProblem.ProblemReportId, listing?.Name ?? string.Empty, $"/admin/problem-reports/{lastPaidProblem.ProblemReportId}", probPaid, lastPaidProblem.PaidUsd, lastPaidProblem.PaidXmr, lastPaidProblem.Status == Data.Enums.ProblemReportStatus.Pending, lastPaidProblem.Status.ToString()));
            }

            this.ViewBag.LastPaidItems = lastPaidItems;
            this.ViewBag.VideoAttention = await this.videoSponsorshipRepository.GetPaymentAttentionAsync();

            // "Newest waiting" = create time of the newest item NOT yet handled (null when none).
            this.ViewBag.NewestPendingSubmissionUtc = await this.submissionRepository.GetNewestPendingUtcAsync();
            this.ViewBag.NewestPendingProblemUtc = await this.problemReportRepository.GetNewestPendingUtcAsync();
            this.ViewBag.NewestPendingVerificationUtc = await this.verificationRequestRepository.GetNewestPendingUtcAsync();
            var newestPendingReview = await this.directoryEntryReviewRepository.Query()
                .Where(r => (r.ModerationStatus == ReviewModerationStatus.Pending || r.ModerationStatus == ReviewModerationStatus.Flagged))
                .MaxAsync(r => (DateTime?)r.CreateDate);
            var newestPendingComment = await this.directoryEntryReviewCommentRepository.Query()
                .Where(c => (c.ModerationStatus == ReviewModerationStatus.Pending || c.ModerationStatus == ReviewModerationStatus.Flagged))
                .MaxAsync(c => (DateTime?)c.CreateDate);
            this.ViewBag.NewestPendingReviewUtc = new[] { newestPendingReview, newestPendingComment }.Max();
            // Public-site (static mirror) status for the dashboard comes from the publish log.
            this.ViewBag.StaticPublishLastInstant = await this.staticPublishRepository.GetLastInstantPublishAsync();
            this.ViewBag.StaticPublishLastFullCrawl = await this.staticPublishRepository.GetLastFullCrawlAsync();
            var publishQueue = await this.staticPublishRepository.ListQueueAsync();
            this.ViewBag.StaticPublishWaiting = publishQueue.Count(q => q.Path != DirectoryManager.Data.Models.StaticPublishing.StaticPublishConstants.FullCrawlPath && q.Attempts == 0);
            this.ViewBag.StaticPublishFailing = publishQueue.Count(q => q.Path != DirectoryManager.Data.Models.StaticPublishing.StaticPublishConstants.FullCrawlPath && q.Attempts > 0);
            this.ViewBag.StaticPublishWorkerPollUtc = DirectoryManager.Web.Services.Implementations.StaticPublishWorkerStatus.LastPollUtc;
            this.ViewBag.StaticPublishMode = this.staticPublishOptions.CurrentValue.Mode;

            return this.View();
        }

        [Route("account/edit")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public IActionResult Edit()
        {
            return this.View();
        }

        [Route("account/changepassword")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return this.View(nameof(this.ChangePassword), new ChangePasswordModel());
        }

        [Route("account/changepassword")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpPost]
        public async Task<IActionResult> ChangePasswordAsync(ChangePasswordModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var currentUser = await this.userManager.GetUserAsync(this.HttpContext.User);
            if (currentUser == null)
            {
                this.ModelState.AddModelError(string.Empty, "Unable to retrieve current user.");
                return this.View(model);
            }

            // The current password is checked with the same lockout as sign-in, so a stolen
            // session cannot be used to guess the password here without limit.
            var check = await this.signInManager.CheckPasswordSignInAsync(currentUser, model.CurrentPassword ?? string.Empty, lockoutOnFailure: true);
            if (!check.Succeeded)
            {
                this.ModelState.AddModelError(string.Empty, check.IsLockedOut ? "Too many wrong passwords. Please wait a few minutes and try again." : "The current password is not correct.");
                return this.View(model);
            }

            var result = await this.userManager.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                this.ModelState.AddModelError(string.Empty, string.Join(",", result.Errors.Select(x => x.Description)));
                return this.View(model);
            }

            // The password change renewed the security stamp (other sessions end); keep this one.
            await this.signInManager.RefreshSignInAsync(currentUser);

            return this.RedirectToAction("Home", "Account");
        }

        // 🔒 POST-only so CSRF (now globally enforced) protects logout.
        [Route("account/logout")]
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public async Task<IActionResult> Logout()
        {
            // Changing the security stamp makes every copy of this admin's sign-in cookie
            // invalid, not just the one in this browser.
            var user = await this.userManager.GetUserAsync(this.User);
            if (user != null)
            {
                await this.userManager.UpdateSecurityStampAsync(user);
            }

            await this.signInManager.SignOutAsync();
            return this.RedirectToAction(nameof(this.Login), "Account");
        }
    }
}