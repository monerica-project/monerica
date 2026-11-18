using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Interfaces;
using DirectoryManager.Web.Tests.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryManager.Web.Tests.Members
{
    public class MemberAccountControllerTests
    {
        private static readonly DirectoryManager.Web.Helpers.MemberAssignedUsername Assigned =
            new (new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());

        [Fact]
        public async Task Register_WithCaptcha_CreatesMember_SignsInWithTheMemberCookieOnly()
        {
            using var h = new IdentityHarness();
            var controller = Controller(h, captchaOk: true);

            var result = await controller.RegisterPost(new MemberRegisterViewModel
            {
                UsernameToken = Assigned.TokenFor("quiet_otter_4821"),
                Password = "a long enough passphrase",
                SavedCredentials = true,
                ReturnUrl = "/account/home", // not a member page: dropped
            });

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Registered", view.ViewName);
            Assert.Equal(MemberAuth.AccountPath, controller.ViewBag.ContinueUrl as string);
            Assert.NotNull(await h.UserManager.FindByNameAsync("quiet_otter_4821"));

            var setCookie = string.Join("\n", h.HttpContext.Response.Headers.SetCookie.ToArray());
            Assert.Contains(MemberAuth.CookieName + "=", setCookie);
            Assert.Contains("path=/members", setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".AspNetCore.Identity.Application", setCookie);
        }

        [Fact]
        public async Task Register_WithoutCaptchaOrSavedTick_CreatesNothing()
        {
            using var h = new IdentityHarness();
            var model = new MemberRegisterViewModel { UsernameToken = Assigned.TokenFor("quiet_otter_4821"), Password = "a long enough passphrase", SavedCredentials = true };

            await Controller(h, captchaOk: false).RegisterPost(model);
            Assert.Null(await h.UserManager.FindByNameAsync("quiet_otter_4821"));

            model.SavedCredentials = false;
            await Controller(h, captchaOk: true).RegisterPost(model);
            Assert.Null(await h.UserManager.FindByNameAsync("quiet_otter_4821"));
        }

        [Fact]
        public async Task SignIn_AdminCredentialsAreRefused_MemberGoesOnlyToMemberPages()
        {
            using var h = new IdentityHarness();
            await h.CreateUserAsync("owner@example.com");
            await h.CreateMemberAsync("swift_fox_7777");

            var asAdmin = Controller(h, captchaOk: true);
            var refused = await asAdmin.SignInPost(new MemberSignInViewModel { Username = "owner@example.com", Password = IdentityHarness.Password });
            Assert.IsType<ViewResult>(refused);
            Assert.False(asAdmin.ModelState.IsValid);
            Assert.Empty(h.HttpContext.Response.Headers.SetCookie.ToArray());

            var asMember = Controller(h, captchaOk: true);
            var ok = await asMember.SignInPost(new MemberSignInViewModel
            {
                Username = "swift_fox_7777",
                Password = IdentityHarness.MemberPassword,
                ReturnUrl = "/account/two-factor",
            });
            Assert.Equal(MemberAuth.AccountPath, Assert.IsType<LocalRedirectResult>(ok).Url);

            var withWrite = await Controller(h, captchaOk: true).SignInPost(new MemberSignInViewModel
            {
                Username = "swift_fox_7777",
                Password = IdentityHarness.MemberPassword,
                ReturnUrl = "/members/reviews/write/alpha-swap",
            });
            Assert.Equal("/members/reviews/write/alpha-swap", Assert.IsType<LocalRedirectResult>(withWrite).Url);
        }

        [Fact]
        public async Task RegisterPage_SuggestsCredentials_WithoutStoringAnything()
        {
            using var h = new IdentityHarness();
            var usersBefore = h.UserManager.Users.Count();
            var view = Assert.IsType<ViewResult>(await Controller(h, captchaOk: false).Register("/members/reviews/write/alpha-swap"));
            var model = Assert.IsType<MemberRegisterViewModel>(view.Model);

            Assert.False(string.IsNullOrEmpty(model.Username));
            Assert.Equal(model.Username, Assigned.Read(model.UsernameToken));
            Assert.True(model.Password!.Length >= 20);
            Assert.Equal("/members/reviews/write/alpha-swap", model.ReturnUrl);
            Assert.Equal(usersBefore, h.UserManager.Users.Count());
            Assert.Empty(h.HttpContext.Response.Headers.SetCookie.ToArray());
        }

        [Fact]
        public async Task Register_UsernameCannotBeChosen_OnlyTheAssignedOneIsAccepted()
        {
            using var h = new IdentityHarness();

            // Typing a name into the form (no token, or a made-up token) creates nothing and
            // hands back a newly assigned name.
            foreach (var token in new[] { null, "made-up-token", Assigned.TokenFor("quiet_otter_4821") + "x" })
            {
                var controller = Controller(h, captchaOk: true);
                var model = new MemberRegisterViewModel { Username = "tony_blanco", UsernameToken = token, Password = "a long enough passphrase", SavedCredentials = true };
                var view = Assert.IsType<ViewResult>(await controller.RegisterPost(model));
                Assert.Equal("Register", view.ViewName);
                Assert.Null(await h.UserManager.FindByNameAsync("tony_blanco"));
                Assert.NotEqual("tony_blanco", model.Username);
                Assert.Equal(model.Username, Assigned.Read(model.UsernameToken));
            }

            // With a valid token the assigned name wins over whatever "Username" was posted.
            var ok = Controller(h, captchaOk: true);
            await ok.RegisterPost(new MemberRegisterViewModel { Username = "tony_blanco", UsernameToken = Assigned.TokenFor("quiet_otter_4821"), Password = "a long enough passphrase", SavedCredentials = true });
            Assert.Null(await h.UserManager.FindByNameAsync("tony_blanco"));
            Assert.NotNull(await h.UserManager.FindByNameAsync("quiet_otter_4821"));

            // The same assigned name a second time (already taken): a new one is assigned.
            var again = Controller(h, captchaOk: true);
            var retry = new MemberRegisterViewModel { UsernameToken = Assigned.TokenFor("quiet_otter_4821"), Password = "a long enough passphrase", SavedCredentials = true };
            Assert.Equal("Register", Assert.IsType<ViewResult>(await again.RegisterPost(retry)).ViewName);
            Assert.NotEqual("quiet_otter_4821", retry.Username);
            Assert.Equal(retry.Username, Assigned.Read(retry.UsernameToken));
        }

        private static MemberAccountController Controller(IdentityHarness h, bool captchaOk)
        {
            h.HttpContext.Response.Headers.Remove("Set-Cookie");
            var controller = new MemberAccountController(h.Members, new FixedCaptcha(captchaOk), h.Services.GetRequiredService<IMemoryCache>(), Assigned)
            {
                ControllerContext = new ControllerContext { HttpContext = h.HttpContext },
            };
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(h.HttpContext, new NullTempData());
            return controller;
        }

        private sealed class FixedCaptcha : ICaptchaService
        {
            private readonly bool ok;

            public FixedCaptcha(bool ok) => this.ok = ok;

            public bool IsValid(HttpRequest request) => this.ok;
        }

        private sealed class NullTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }
    }
}
