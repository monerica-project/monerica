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
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IAffiliateCommissionRepository affiliateCommissionRepository;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository directoryEntryRepository,
            ISubmissionRepository submissionRepository,
            IDirectoryEntryReviewRepository directoryEntryReviewRepository,
            IDirectoryEntryReviewCommentRepository directoryEntryReviewCommentRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IAffiliateCommissionRepository affiliateCommissionRepository,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.directoryEntryRepository = directoryEntryRepository;
            this.submissionRepository = submissionRepository;
            this.directoryEntryReviewRepository = directoryEntryReviewRepository;
            this.directoryEntryReviewCommentRepository = directoryEntryReviewCommentRepository;
            this.affiliateCommissionRepository = affiliateCommissionRepository;
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

            var result = await this.signInManager.PasswordSignInAsync(username, password, rememberMe, false);

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
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Pending)
                .CountAsync();

            // Pending replies/comments
            var pendingReviewComments = await this.directoryEntryReviewCommentRepository
                .Query()
                .Where(c => c.ModerationStatus == ReviewModerationStatus.Pending)
                .CountAsync();

            // Combined total for your dashboard badge
            var totalPendingReviewItems = pendingReviews + pendingReviewComments;

            var pendingAffiliateCommissions =
                await this.affiliateCommissionRepository.CountByStatusAsync(CommissionPayoutStatus.Pending);

            this.ViewBag.TotalPendingSubmissions = totalPendingSubmissions;

            // ✅ keep your existing name, but now includes BOTH
            this.ViewBag.TotalPendingReviews = totalPendingReviewItems;

            // ✅ optional breakdown (use if you want)
            this.ViewBag.TotalPendingReviewReviews = pendingReviews;
            this.ViewBag.TotalPendingReviewComments = pendingReviewComments;

            this.ViewBag.PendingAffiliateCommissions = pendingAffiliateCommissions;

            return this.View();
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

        [Route("account/logout")]
        public async Task<IActionResult> Logout()
        {
            await this.signInManager.SignOutAsync();
            return this.RedirectToAction(nameof(this.Login), "Account");
        }
    }
}