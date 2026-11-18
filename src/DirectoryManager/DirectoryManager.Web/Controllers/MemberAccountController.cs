using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Member accounts: the low-friction way to write reviews (username + password, no email).
    /// The PGP review flow stays the default and is unchanged. Members use their own cookie
    /// scheme, so nothing here can sign anyone in as an admin.
    /// </summary>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class MemberAccountController : BaseController
    {
        public const string FailedSignInMessage =
            "Wrong username or password. After several failed attempts, sign-in is paused for a few minutes.";

        private const string RegisterCaptcha = "member-register";
        private const string SignInCaptcha = "member-signin";

        private readonly MemberAccountService accounts;
        private readonly ICaptchaService captcha;
        private readonly MemberAssignedUsername assignedUsername;

        public MemberAccountController(MemberAccountService accounts, ICaptchaService captcha, IMemoryCache cache, MemberAssignedUsername assignedUsername)
            : base(cache)
        {
            this.accounts = accounts;
            this.captcha = captcha;
            this.assignedUsername = assignedUsername;
        }

        [HttpGet("members/register")]
        public async Task<IActionResult> Register(string? returnUrl = null)
        {
            var safeReturn = MemberReturnUrl.Safe(returnUrl);
            if (await this.CurrentMemberAsync() != null)
            {
                return this.LocalRedirect(safeReturn ?? MemberAuth.AccountPath);
            }

            this.ViewBag.RegistrationOpen = MemberAuth.RegistrationEnabled;

            // Suggestions are only written into this page; nothing is stored until the form is
            // submitted with a solved captcha.
            // The username is assigned, not chosen: it is shown as text and travels in a signed token.
            var (username, token) = this.assignedUsername.Issue();
            return this.View(new MemberRegisterViewModel
            {
                Username = username,
                UsernameToken = token,
                Password = MemberCredentialGenerator.Password(),
                ReturnUrl = safeReturn,
            });
        }

        [HttpPost("members/register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPost(MemberRegisterViewModel model)
        {
            model.ReturnUrl = MemberReturnUrl.Safe(model.ReturnUrl);
            this.ViewBag.RegistrationOpen = MemberAuth.RegistrationEnabled;

            // Only a username the site assigned is accepted. Whatever was posted as "Username" is
            // ignored; a missing, altered or expired token gets a new assigned name.
            var assigned = this.assignedUsername.Read(model.UsernameToken);
            if (assigned == null)
            {
                this.AssignNewUsername(model);
                this.ModelState.AddModelError(string.Empty, "This page was open too long, so a new username was assigned. Save it with your password, then continue.");
                model.SavedCredentials = false;
                this.ModelState.Remove(nameof(model.SavedCredentials));
                return this.View(nameof(this.Register), model);
            }

            model.Username = assigned;

            if (!MemberAuth.RegistrationEnabled)
            {
                return this.View(nameof(this.Register), model);
            }

            // Captcha first: no account lookup or other work happens for a bot.
            if (!this.captcha.IsValid(this.Request))
            {
                this.ModelState.AddModelError(string.Empty, "Invalid CAPTCHA. Please try again.");
                return this.View(nameof(this.Register), model);
            }

            if (!model.SavedCredentials)
            {
                this.ModelState.AddModelError(nameof(model.SavedCredentials), "Please save your username and password first, then tick the box. Without an email there is no way to recover them.");
                return this.View(nameof(this.Register), model);
            }

            var result = await this.accounts.RegisterAsync(model.Username, model.Password);
            if (!result.Succeeded)
            {
                var usernameProblem = false;
                foreach (var (field, message) in result.Errors)
                {
                    if (field == nameof(model.Username))
                    {
                        usernameProblem = true;
                        continue;
                    }

                    this.ModelState.AddModelError(field, message);
                }

                if (usernameProblem)
                {
                    // The assigned name was taken in the meantime (or is no longer allowed): give another.
                    this.AssignNewUsername(model);
                    this.ModelState.AddModelError(string.Empty, "That username was just taken, so a new one was assigned. Save it with your password, then continue.");
                    model.SavedCredentials = false;
                    this.ModelState.Remove(nameof(model.SavedCredentials));
                }

                return this.View(nameof(this.Register), model);
            }

            await this.SignInMemberAsync(result.User!, persistent: false);

            // Shown once, straight from this POST (never stored, never redirected with it).
            this.ViewBag.ContinueUrl = model.ReturnUrl ?? MemberAuth.AccountPath;
            return this.View("Registered", new MemberRegisterViewModel
            {
                Username = result.User!.UserName,
                Password = model.Password,
            });
        }

        private void AssignNewUsername(MemberRegisterViewModel model)
        {
            var (username, token) = this.assignedUsername.Issue();
            model.Username = username;
            model.UsernameToken = token;
            this.ModelState.Remove(nameof(model.Username));
            this.ModelState.Remove(nameof(model.UsernameToken));
        }

        [HttpGet("members/sign-in")]
        public async Task<IActionResult> SignIn(string? returnUrl = null)
        {
            var safeReturn = MemberReturnUrl.Safe(returnUrl);
            if (await this.CurrentMemberAsync() != null)
            {
                return this.LocalRedirect(safeReturn ?? MemberAuth.AccountPath);
            }

            return this.View(new MemberSignInViewModel { ReturnUrl = safeReturn });
        }

        [HttpPost("members/sign-in")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignInPost(MemberSignInViewModel model)
        {
            model.ReturnUrl = MemberReturnUrl.Safe(model.ReturnUrl);

            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                this.ModelState.AddModelError(string.Empty, "Please enter your username and password.");
                return this.View(nameof(this.SignIn), model);
            }

            if (!this.captcha.IsValid(this.Request))
            {
                this.ModelState.AddModelError(string.Empty, "Invalid CAPTCHA. Please try again.");
                return this.View(nameof(this.SignIn), model);
            }

            var user = await this.accounts.CheckSignInAsync(model.Username, model.Password);
            if (user == null)
            {
                // One message for every failure, so nobody learns whether a username exists.
                this.ModelState.AddModelError(string.Empty, FailedSignInMessage);
                model.Password = null;
                return this.View(nameof(this.SignIn), model);
            }

            await this.SignInMemberAsync(user, model.RememberMe);
            return this.LocalRedirect(model.ReturnUrl ?? MemberAuth.AccountPath);
        }

        [HttpPost("members/sign-out")]
        [Authorize(Policy = AuthorizationPolicies.Member)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOutPost()
        {
            await this.HttpContext.SignOutAsync(MemberAuth.Scheme);
            return this.LocalRedirect(MemberAuth.SignInPath);
        }

        [HttpGet("members/account")]
        [Authorize(Policy = AuthorizationPolicies.Member)]
        public async Task<IActionResult> Account(CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var db = this.HttpContext.RequestServices.GetRequiredService<DirectoryManager.Data.DbContextInfo.ApplicationDbContext>();
            return this.View(new MemberAccountViewModel
            {
                Username = user.UserName ?? string.Empty,
                MemberSinceUtc = user.CreateDate,
                ReviewCount = await db.DirectoryEntryReviews.CountAsync(r => r.AuthorUserId == user.Id, ct),
                ReplyCount = await db.DirectoryEntryReviewComments.CountAsync(c => c.AuthorUserId == user.Id, ct),
                PgpFingerprint = user.PgpFingerprint,
            });
        }

        [HttpGet("members/change-password")]
        [Authorize(Policy = AuthorizationPolicies.Member)]
        public IActionResult ChangePassword() => this.View(new MemberChangePasswordViewModel());

        [HttpPost("members/change-password")]
        [Authorize(Policy = AuthorizationPolicies.Member)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordPost(MemberChangePasswordViewModel model)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var result = await this.accounts.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                {
                    var field = e.Code == "PasswordMismatch" ? nameof(model.CurrentPassword) : nameof(model.NewPassword);
                    this.ModelState.AddModelError(field, e.Code == "PasswordMismatch" ? "Your current password is not correct." : e.Description);
                }

                return this.View(nameof(this.ChangePassword), new MemberChangePasswordViewModel());
            }

            // The security stamp changed (other devices are signed out); renew this device's cookie.
            await this.SignInMemberAsync(user, persistent: false);
            this.TempData["MemberMessage"] = "Your password was changed. Save the new one: it can't be recovered.";
            return this.LocalRedirect(MemberAuth.AccountPath);
        }

        [HttpGet("members/delete")]
        [Authorize(Policy = AuthorizationPolicies.Member)]
        public IActionResult Delete() => this.View(new MemberDeleteAccountViewModel());

        [HttpPost("members/delete")]
        [Authorize(Policy = AuthorizationPolicies.Member)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(MemberDeleteAccountViewModel model, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            if (!model.Confirm)
            {
                this.ModelState.AddModelError(nameof(model.Confirm), "Tick the box to confirm.");
                return this.View(nameof(this.Delete), new MemberDeleteAccountViewModel());
            }

            if (!await this.accounts.DeleteAccountAsync(user, model.Password, ct))
            {
                this.ModelState.AddModelError(nameof(model.Password), "That password is not correct.");
                return this.View(nameof(this.Delete), new MemberDeleteAccountViewModel());
            }

            await this.HttpContext.SignOutAsync(MemberAuth.Scheme);
            this.ClearCachedItems();
            return this.View("Deleted");
        }

        private async Task<ApplicationUser?> CurrentMemberAsync()
        {
            var auth = await this.HttpContext.AuthenticateAsync(MemberAuth.Scheme);
            return auth.Succeeded ? await this.accounts.FindValidMemberAsync(auth.Principal) : null;
        }

        private Task SignInMemberAsync(ApplicationUser user, bool persistent) =>
            this.HttpContext.SignInAsync(
                MemberAuth.Scheme,
                this.accounts.CreatePrincipal(user),
                new AuthenticationProperties
                {
                    IsPersistent = persistent,
                    AllowRefresh = true,
                });
    }
}
