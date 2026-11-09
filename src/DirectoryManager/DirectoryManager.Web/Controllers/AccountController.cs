using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
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
            IMemoryCache cache)
            : base(cache)
        {
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
        public async Task<IActionResult> Login(string username, string password, bool rememberMe)
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

            // 🔒 lockoutOnFailure: true engages Identity's failed-attempt counter
            var result = await this.signInManager.PasswordSignInAsync(
                username, password, rememberMe, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                // Generic message — don't reveal whether the user exists
                this.ModelState.AddModelError("", "Too many attempts. Please try again later.");
                return this.View();
            }

            if (result.IsNotAllowed)
            {
                this.ModelState.AddModelError("", "Login not allowed.");
                return this.View();
            }

            if (result.Succeeded)
            {
                return this.RedirectToAction("Home", "Account");
            }

            this.ModelState.AddModelError("", "Invalid login attempt.");
            return this.View();
        }

        [Route("account/home")]
        [Authorize]
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
            // The Bunny syncer stamps two files: last-success.txt on every OK run
            // ("last checked"), and last-sync.txt only when it actually uploaded changed
            // files ("last files synced"). Both are written by the syncer's systemd unit.
            this.ViewBag.LastBunnyCheckedUtc = ReadStampUtc("/var/lib/gitsitesyncer-monerica-bunny/last-success.txt");
            this.ViewBag.LastBunnyFilesSyncedUtc = ReadStampUtc("/var/lib/gitsitesyncer-monerica-bunny/last-sync.txt");

            return this.View();
        }

        // Reads an ISO-8601 UTC timestamp the syncer stamps into a file. Best-effort:
        // returns null if the file is missing/unreadable so the dashboard just omits
        // that status rather than erroring (the syncer is a separate service).
        private static DateTime? ReadStampUtc(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }

                var text = System.IO.File.ReadAllText(path).Trim();
                if (System.DateTimeOffset.TryParse(
                        text,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var dto))
                {
                    return dto.UtcDateTime;
                }
            }
            catch
            {
                // Ignore — dashboard shows "status unavailable" if it can't be read.
            }

            return null;
        }

        [Route("account/edit")]
        [Authorize]
        public IActionResult Edit()
        {
            return this.View();
        }

        [Route("account/changepassword")]
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return this.View(nameof(this.ChangePassword), new ChangePasswordModel());
        }

        [Route("account/changepassword")]
        [Authorize]
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

            var result = await this.userManager.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                this.ModelState.AddModelError(string.Empty, string.Join(",", result.Errors.Select(x => x.Description)));
                return this.View(model);
            }

            return this.RedirectToAction("Home", "Account");
        }

        // 🔒 POST-only so CSRF (now globally enforced) protects logout.
        [Route("account/logout")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await this.signInManager.SignOutAsync();
            return this.RedirectToAction(nameof(this.Login), "Account");
        }
    }
}