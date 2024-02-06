using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISubmissionRepository submissionRepository;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository directoryEntryRepository,
            ISubmissionRepository submissionRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.directoryEntryRepository = directoryEntryRepository;
            this.submissionRepository = submissionRepository;
        }

        public IActionResult Login()
        {
            return this.View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                // Add an error message and return to the login page.
                this.ModelState.AddModelError("", "Username and password are required.");
                return this.View();
            }

            var result = await this.signInManager.PasswordSignInAsync(username, password, rememberMe, false);

            if (result.Succeeded)
            {
                return this.RedirectToAction("Home", "Account");
            }
            else
            {
                // Add an error message and return to the login page.
                this.ModelState.AddModelError("", "Invalid login attempt.");
                return this.View();
            }
        }

        [Authorize]
        public async Task<IActionResult> HomeAsync()
        {
            var totalPendingSubmissions = await this.submissionRepository.GetByStatus(Data.Enums.SubmissionStatus.Pending);

            this.ViewBag.TotalPendingSubmissions = totalPendingSubmissions;

            return this.View();
        }

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

        public async Task<IActionResult> Logout()
        {
            await this.signInManager.SignOutAsync();
            return this.RedirectToAction(nameof(this.Login), "Account");
        }
    }
}