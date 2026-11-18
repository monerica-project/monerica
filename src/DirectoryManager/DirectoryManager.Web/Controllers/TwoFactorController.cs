using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin pages to turn authenticator-app (TOTP) two-factor login on and off and to replace the
    /// recovery codes. Uses ASP.NET Core Identity's built-in authenticator key and recovery-code
    /// storage (AspNetUserTokens + AspNetUsers.TwoFactorEnabled), so no schema change.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class TwoFactorController : Controller
    {
        public const int RecoveryCodeCount = 10;

        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly AdminLoginService adminLoginService;
        private readonly ICacheService cacheService;

        public TwoFactorController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AdminLoginService adminLoginService,
            ICacheService cacheService)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.adminLoginService = adminLoginService;
            this.cacheService = cacheService;
        }

        [HttpGet]
        [Route("account/two-factor")]
        public async Task<IActionResult> Index()
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            var vm = new TwoFactorStatusViewModel
            {
                IsEnabled = user.TwoFactorEnabled,
                RecoveryCodesLeft = user.TwoFactorEnabled ? await this.userManager.CountRecoveryCodesAsync(user) : 0,
            };
            return this.View(vm);
        }

        [HttpGet]
        [Route("account/two-factor/enable")]
        public async Task<IActionResult> Enable()
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            if (user.TwoFactorEnabled)
            {
                // Never swap the key of a working setup from a GET.
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(await this.BuildEnableModelAsync(user));
        }

        [HttpPost]
        [Route("account/two-factor/enable")]
        public async Task<IActionResult> Enable(string? code)
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            if (user.TwoFactorEnabled)
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            var totp = AdminLoginService.NormalizeTotp(code);
            if (totp == null || !await this.adminLoginService.VerifyTotpAsync(user, totp))
            {
                this.ModelState.AddModelError(string.Empty, "That code is not valid. Check the time on your phone and enter the current 6-digit code.");
                return this.View(await this.BuildEnableModelAsync(user));
            }

            var result = await this.userManager.SetTwoFactorEnabledAsync(user, true);
            if (!result.Succeeded)
            {
                this.ModelState.AddModelError(string.Empty, "Could not turn on two-factor login: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                return this.View(await this.BuildEnableModelAsync(user));
            }

            var codes = await this.userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount) ?? Enumerable.Empty<string>();

            // Enabling changes the security stamp; refresh so this session stays signed in.
            await this.signInManager.RefreshSignInAsync(user);

            return this.View("RecoveryCodes", new TwoFactorRecoveryCodesViewModel { Codes = codes.ToList(), JustEnabled = true });
        }

        [HttpGet]
        [Route("account/two-factor/disable")]
        public async Task<IActionResult> Disable()
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            if (!user.TwoFactorEnabled)
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View();
        }

        [HttpPost]
        [Route("account/two-factor/disable")]
        public async Task<IActionResult> Disable(string? currentPassword, string? code)
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            if (!user.TwoFactorEnabled)
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            if (!await this.CheckPasswordAndCodeAsync(user, currentPassword, code, allowRecoveryCode: true))
            {
                this.ModelState.AddModelError(string.Empty, "The password or code is not correct.");
                return this.View();
            }

            await this.userManager.SetTwoFactorEnabledAsync(user, false);

            // Forget the old key and codes so a later re-enable starts fresh.
            await this.userManager.ResetAuthenticatorKeyAsync(user);
            await this.userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
            await this.signInManager.RefreshSignInAsync(user);

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet]
        [Route("account/two-factor/recovery-codes")]
        public async Task<IActionResult> RegenerateRecoveryCodes()
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            if (!user.TwoFactorEnabled)
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View();
        }

        [HttpPost]
        [Route("account/two-factor/recovery-codes")]
        public async Task<IActionResult> RegenerateRecoveryCodes(string? currentPassword, string? code)
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Challenge();
            }

            if (!user.TwoFactorEnabled)
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            // Authenticator code only: a recovery code shouldn't be able to mint a new set.
            if (!await this.CheckPasswordAndCodeAsync(user, currentPassword, code, allowRecoveryCode: false))
            {
                this.ModelState.AddModelError(string.Empty, "The password or authenticator code is not correct.");
                return this.View();
            }

            var codes = await this.userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount) ?? Enumerable.Empty<string>();
            return this.View("RecoveryCodes", new TwoFactorRecoveryCodesViewModel { Codes = codes.ToList(), JustEnabled = false });
        }

        /// <summary>otpauth:// URI an authenticator app reads from the QR code (RFC 6238 defaults).</summary>
        public static string BuildAuthenticatorUri(string issuer, string accountName, string unformattedKey)
        {
            var i = Uri.EscapeDataString(issuer);
            var a = Uri.EscapeDataString(accountName);
            return $"otpauth://totp/{i}:{a}?secret={unformattedKey}&issuer={i}&digits=6";
        }

        /// <summary>"ABCDEFGH..." → "abcd efgh ..." so it is easy to type into an app.</summary>
        public static string FormatKey(string unformattedKey)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < unformattedKey.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    sb.Append(' ');
                }

                sb.Append(unformattedKey[i]);
            }

            return sb.ToString().ToLowerInvariant();
        }

        private static string QrDataUri(string text)
        {
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var png = new QRCoder.PngByteQRCode(data);
            var bytes = png.GetGraphic(6);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private async Task<bool> CheckPasswordAndCodeAsync(ApplicationUser user, string? password, string? code, bool allowRecoveryCode)
        {
            bool passwordOk = !string.IsNullOrEmpty(password) && await this.userManager.CheckPasswordAsync(user, password);
            if (!passwordOk)
            {
                return false;
            }

            return await this.adminLoginService.VerifySecondFactorAsync(user, code, allowRecoveryCode);
        }

        private async Task<TwoFactorEnableViewModel> BuildEnableModelAsync(ApplicationUser user)
        {
            var key = await this.userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await this.userManager.ResetAuthenticatorKeyAsync(user);
                key = await this.userManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;

                // Resetting the key changes the security stamp; keep this session valid.
                await this.signInManager.RefreshSignInAsync(user);
            }

            var issuer = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
            if (string.IsNullOrWhiteSpace(issuer))
            {
                issuer = this.Request.Host.Host;
            }

            var account = user.UserName ?? user.Email ?? "admin";
            var uri = BuildAuthenticatorUri(issuer.Trim(), account, key);

            return new TwoFactorEnableViewModel
            {
                SharedKey = FormatKey(key),
                AuthenticatorUri = uri,
                QrCodeDataUri = QrDataUri(uri),
                Issuer = issuer.Trim(),
                AccountName = account,
            };
        }
    }
}
