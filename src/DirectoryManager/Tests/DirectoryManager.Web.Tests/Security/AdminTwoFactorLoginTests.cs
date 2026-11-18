using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace DirectoryManager.Web.Tests.Security
{
    /// <summary>
    /// Admin login with optional authenticator-app 2FA, and the lockout fix: with 2FA on, the
    /// correct password + a valid code gets in even while strangers keep the account locked.
    /// </summary>
    public class AdminTwoFactorLoginTests
    {
        // ---------------- 2FA off: unchanged behavior ----------------
        [Fact]
        public async Task TwoFactorOff_CorrectPassword_Succeeds_WithoutCode()
        {
            using var h = new IdentityHarness();
            await h.CreateUserAsync();

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, null);

            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
            Assert.False(r.UsedSecondFactor);
        }

        [Fact]
        public async Task TwoFactorOff_TypedCodeIsIgnored()
        {
            using var h = new IdentityHarness();
            await h.CreateUserAsync();

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, "123456");

            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
        }

        [Fact]
        public async Task TwoFactorOff_WrongPassword_Fails_AndCountsTowardLockout()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();

            var r = await h.Login.CheckAsync("admin", "wrong-password", null);

            Assert.Equal(AdminLoginOutcome.Failed, r.Outcome);
            Assert.Equal(1, await h.UserManager.GetAccessFailedCountAsync(user));
        }

        [Fact]
        public async Task TwoFactorOff_FiveFailures_LockOut_EvenTheCorrectPassword()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            await h.LockOutAsync(user);

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, null);

            Assert.Equal(AdminLoginOutcome.Failed, r.Outcome); // today's behavior kept when 2FA is off
        }

        [Fact]
        public async Task LockedAccount_LooksTheSameAsAnUnknownUser()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            await h.LockOutAsync(user);

            var locked = await h.Login.CheckAsync("admin", "wrong-password", null);
            var unknown = await h.Login.CheckAsync("nobody", "wrong-password", null);

            Assert.Equal(unknown, locked);
            Assert.Equal(AdminLoginOutcome.Failed, locked.Outcome);
            Assert.Null(locked.User);
        }

        // ---------------- 2FA on ----------------
        [Fact]
        public async Task TwoFactorOn_PasswordAlone_Fails()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            await h.EnableTwoFactorAsync(user);

            var noCode = await h.Login.CheckAsync("admin", IdentityHarness.Password, null);
            var wrongCode = await h.Login.CheckAsync("admin", IdentityHarness.Password, "000000");

            Assert.Equal(AdminLoginOutcome.Failed, noCode.Outcome);
            Assert.Equal(AdminLoginOutcome.Failed, wrongCode.Outcome);
            Assert.Equal(2, await h.UserManager.GetAccessFailedCountAsync(user));
        }

        [Fact]
        public async Task TwoFactorOn_PasswordPlusValidCode_Succeeds()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, IdentityHarness.Totp(key));

            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
            Assert.True(r.UsedSecondFactor);
            Assert.Equal("mfa", AdminLoginService.AuthenticationMethod(r));
        }

        [Fact]
        public async Task TwoFactorOn_CodeWithSpaces_IsAccepted()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);
            var code = IdentityHarness.Totp(key);

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, $" {code[..3]} {code[3..]} ");

            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
        }

        [Fact]
        public async Task TwoFactorOn_ValidCodeWithWrongPassword_Fails()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);

            var r = await h.Login.CheckAsync("admin", "wrong-password", IdentityHarness.Totp(key));

            Assert.Equal(AdminLoginOutcome.Failed, r.Outcome);
        }

        [Fact]
        public async Task TwoFactorOn_SameCodeCannotBeReplayed()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);
            var code = IdentityHarness.Totp(key);

            Assert.Equal(AdminLoginOutcome.Success, (await h.Login.CheckAsync("admin", IdentityHarness.Password, code)).Outcome);
            Assert.Equal(AdminLoginOutcome.Failed, (await h.Login.CheckAsync("admin", IdentityHarness.Password, code)).Outcome);
        }

        [Fact]
        public async Task TwoFactorOn_RecoveryCode_WorksExactlyOnce()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (_, codes) = await h.EnableTwoFactorAsync(user);

            var first = await h.Login.CheckAsync("admin", IdentityHarness.Password, codes[0].ToLowerInvariant());
            var second = await h.Login.CheckAsync("admin", IdentityHarness.Password, codes[0]);

            Assert.Equal(AdminLoginOutcome.Success, first.Outcome);
            Assert.Equal(AdminLoginOutcome.Failed, second.Outcome);
            Assert.Equal(9, await h.UserManager.CountRecoveryCodesAsync(user));
        }

        [Fact]
        public async Task TwoFactorOn_RecoveryCode_IsNotSpentByAWrongPassword()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (_, codes) = await h.EnableTwoFactorAsync(user);

            await h.Login.CheckAsync("admin", "wrong-password", codes[0]);

            Assert.Equal(10, await h.UserManager.CountRecoveryCodesAsync(user));
        }

        // ---------------- the lockout fix ----------------
        [Fact]
        public async Task TwoFactorOn_PasswordPlusCode_SucceedsDuringLockout_AndClearsIt()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);
            await h.LockOutAsync(user);

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, IdentityHarness.Totp(key));

            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
            Assert.False(await h.UserManager.IsLockedOutAsync(user));
            Assert.Equal(0, await h.UserManager.GetAccessFailedCountAsync(user));
        }

        [Fact]
        public async Task TwoFactorOn_RecoveryCode_SucceedsDuringLockout()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (_, codes) = await h.EnableTwoFactorAsync(user);
            await h.LockOutAsync(user);

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, codes[3]);

            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
        }

        [Fact]
        public async Task TwoFactorOn_WrongCodeDuringLockout_Fails_AndStillCounts()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            await h.EnableTwoFactorAsync(user);
            await h.LockOutAsync(user);
            int before = await h.UserManager.GetAccessFailedCountAsync(user);

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, "000000");

            Assert.Equal(AdminLoginOutcome.Failed, r.Outcome);
            Assert.True(await h.UserManager.IsLockedOutAsync(user));
            Assert.Equal(before + 1, await h.UserManager.GetAccessFailedCountAsync(user));
        }

        [Fact]
        public async Task TwoFactorOn_CodeChecks_AreLimitedPerTimeStep()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);

            for (int i = 0; i < AdminLoginService.MaxCodeChecksPerStep; i++)
            {
                await h.Login.CheckAsync("admin", IdentityHarness.Password, "000000");
            }

            // Budget for this 30-second step is spent: even the right code is refused...
            var blocked = await h.Login.CheckAsync("admin", IdentityHarness.Password, IdentityHarness.Totp(key));
            Assert.Equal(AdminLoginOutcome.Failed, blocked.Outcome);

            // ...until the next step.
            h.Clock.Advance(TimeSpan.FromSeconds(30));
            var later = await h.Login.CheckAsync("admin", IdentityHarness.Password, IdentityHarness.Totp(key));
            Assert.Equal(AdminLoginOutcome.Success, later.Outcome);
        }

        [Fact]
        public async Task WrongPasswords_DoNotUseUpTheCodeBudget()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);

            for (int i = 0; i < 20; i++)
            {
                await h.Login.CheckAsync("admin", "stranger-guess", "000000");
            }

            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, IdentityHarness.Totp(key));
            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
        }

        // ---------------- enabling from the admin page ----------------
        [Fact]
        public async Task Enable_RequiresAValidCode()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            await h.SignInPrincipalAsync(user);
            var controller = NewController(h);

            var page = Assert.IsType<ViewResult>(await controller.Enable());
            var vm = Assert.IsType<TwoFactorEnableViewModel>(page.Model);
            Assert.StartsWith("data:image/png;base64,", vm.QrCodeDataUri);
            Assert.Contains("issuer=Test%20Directory", vm.AuthenticatorUri);
            var key = await h.UserManager.GetAuthenticatorKeyAsync(user);

            var wrong = Assert.IsType<ViewResult>(await controller.Enable("000000"));
            Assert.IsType<TwoFactorEnableViewModel>(wrong.Model);
            Assert.False(await h.UserManager.GetTwoFactorEnabledAsync(user));

            var right = Assert.IsType<ViewResult>(await controller.Enable(IdentityHarness.Totp(key!)));
            Assert.Equal("RecoveryCodes", right.ViewName);
            var codes = Assert.IsType<TwoFactorRecoveryCodesViewModel>(right.Model);
            Assert.Equal(TwoFactorController.RecoveryCodeCount, codes.Codes.Count);
            Assert.True(await h.UserManager.GetTwoFactorEnabledAsync(user));
        }

        [Fact]
        public async Task Enable_Get_DoesNotReplaceTheKeyOnceOn()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, _) = await h.EnableTwoFactorAsync(user);
            await h.SignInPrincipalAsync(user);

            var result = await NewController(h).Enable();

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(key, await h.UserManager.GetAuthenticatorKeyAsync(user));
        }

        [Fact]
        public async Task Disable_NeedsPasswordAndCode()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (_, codes) = await h.EnableTwoFactorAsync(user);
            await h.SignInPrincipalAsync(user);
            var controller = NewController(h);

            Assert.IsType<ViewResult>(await controller.Disable(IdentityHarness.Password, "000000"));
            Assert.IsType<ViewResult>(await controller.Disable("wrong-password", codes[0]));
            Assert.True(await h.UserManager.GetTwoFactorEnabledAsync(user));

            Assert.IsType<RedirectToActionResult>(await controller.Disable(IdentityHarness.Password, codes[1]));
            Assert.False(await h.UserManager.GetTwoFactorEnabledAsync(user));

            // And the login goes back to password-only.
            var r = await h.Login.CheckAsync("admin", IdentityHarness.Password, null);
            Assert.Equal(AdminLoginOutcome.Success, r.Outcome);
        }

        [Fact]
        public async Task RegenerateRecoveryCodes_ReplacesTheOldSet()
        {
            using var h = new IdentityHarness();
            var user = await h.CreateUserAsync();
            var (key, oldCodes) = await h.EnableTwoFactorAsync(user);
            await h.SignInPrincipalAsync(user);

            var result = Assert.IsType<ViewResult>(await NewController(h).RegenerateRecoveryCodes(IdentityHarness.Password, IdentityHarness.Totp(key)));

            var fresh = Assert.IsType<TwoFactorRecoveryCodesViewModel>(result.Model);
            Assert.Equal(10, fresh.Codes.Count);
            Assert.Empty(fresh.Codes.Intersect(oldCodes));
            Assert.Equal(AdminLoginOutcome.Failed, (await h.Login.CheckAsync("admin", IdentityHarness.Password, oldCodes[0])).Outcome);
        }

        [Fact]
        public void ManagementPages_RequireLogin()
        {
            Assert.NotEmpty(typeof(TwoFactorController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        }

        [Fact]
        public void AuthenticatorUri_UsesTheConfiguredSiteNameAsIssuer()
        {
            var uri = TwoFactorController.BuildAuthenticatorUri("My Site", "admin", "ABCDEFGH");

            Assert.Equal("otpauth://totp/My%20Site:admin?secret=ABCDEFGH&issuer=My%20Site&digits=6", uri);
            Assert.Equal("abcd efgh", TwoFactorController.FormatKey("ABCDEFGH"));
        }

        private static TwoFactorController NewController(IdentityHarness h)
        {
            return new TwoFactorController(h.UserManager, h.SignInManager, h.Login, new FakeCacheService())
            {
                ControllerContext = new ControllerContext(new ActionContext(h.HttpContext, new RouteData(), new ControllerActionDescriptor())),
            };
        }

        private sealed class FakeCacheService : ICacheService
        {
            public void ClearSnippetCache(SiteConfigSetting snippetType)
            {
            }

            public Task<string> GetSnippetAsync(SiteConfigSetting snippetType) =>
                Task.FromResult(snippetType == SiteConfigSetting.SiteName ? "Test Directory" : string.Empty);
        }
    }
}
