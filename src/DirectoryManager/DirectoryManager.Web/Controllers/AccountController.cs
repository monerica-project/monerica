using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly UserManager<ApplicationUser> userManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository directoryEntryRepository,
            ITrafficLogRepository trafficLogRepository)
            : base(trafficLogRepository)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.directoryEntryRepository = directoryEntryRepository;
        }

        public IActionResult Login()
        {
            return this.View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> NewestAdditions(int numberOfDays = 10)
        {
            var groupedNewestAdditions = await this.directoryEntryRepository.GetNewestAdditionsGrouped(numberOfDays);

            var viewModel = groupedNewestAdditions.SelectMany(group =>
                group.Entries.Select(entry => new GroupedDirectoryEntry
                {
                    Date = group.Date,
                    Entries = new List<DirectoryEntry> // Create a list to store entries
                    {
                    new DirectoryEntry
                    {
                        Name = entry.Name,
                        Link = entry.Link,
                        Description = entry.Description
                    }
                    }
                }));

            return this.View(viewModel);
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
                return this.RedirectToAction(nameof(this.Home), "Account");
            }
            else
            {
                // Add an error message and return to the login page.
                this.ModelState.AddModelError("", "Invalid login attempt.");
                return this.View();
            }
        }

        [Authorize]
        public IActionResult Home()
        {
            return this.View();
        }

        [Authorize]
        public IActionResult Edit()
        {
            return this.View();
        }

        public async Task<IActionResult> Logout()
        {
            await this.signInManager.SignOutAsync();
            return this.RedirectToAction(nameof(this.Login), "Account");
        }
    }
}