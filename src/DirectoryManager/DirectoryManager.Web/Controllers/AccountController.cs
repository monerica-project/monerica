using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        public IDirectoryEntryRepository _directoryEntryRepository;

        public AccountController(
            SignInManager<ApplicationUser> signInManager, 
            UserManager<ApplicationUser> userManager,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _directoryEntryRepository = directoryEntryRepository;
        }


        public IActionResult Login()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> NewestAdditions(int numberOfDays = 10)
        {
            var groupedNewestAdditions = await _directoryEntryRepository.GetNewestAdditionsGrouped(numberOfDays);

            var viewModel = groupedNewestAdditions.SelectMany(group =>
                group.Entries.Select(entry => new GroupedDirectoryEntry
                {
                    Date = group.Date,
                    Name = entry.Name,
                    Entries = new List<DirectoryEntry> // Create a list to store entries
                    {
                    new DirectoryEntry
                    {
                        Name = entry.Name,
                        Link = entry.Link,
                        Description = entry.Description
                    }
                    }
                })
            );

            return View(viewModel);
        }


        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                // Add an error message and return to the login page.
                ModelState.AddModelError("", "Username and password are required.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(username, password, rememberMe, false);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Home), "Account");
            }
            else
            {
                // Add an error message and return to the login page.
                ModelState.AddModelError("", "Invalid login attempt.");
                return View();
            }
        }

        [Authorize]
        public IActionResult Home()
        {
            return View();
        }

        [Authorize]
        public IActionResult Edit()
        {
            return View();
        }
        
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login), "Account");
        }
    }
}