using System.Security.Cryptography;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryManager.Web.Tests.Security
{
    /// <summary>
    /// Real ASP.NET Core Identity (same lockout options as the site) over an in-memory database,
    /// for testing the admin login and two-factor code paths without a browser or Postgres.
    /// </summary>
    public sealed class IdentityHarness : IDisposable
    {
        public const string Password = "Correct-Horse-Battery-9";

        public const string MemberPassword = "plain words pass";

        public const string SiteName = "Example Directory";

        private readonly ServiceProvider root;
        private readonly IServiceScope scope;

        public IdentityHarness()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddControllersWithViews(); // TempData/Url helpers that Controller.View()/Redirect use
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase($"identity-{Guid.NewGuid():N}"));
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = false; // member accounts have no email (same as the site)
            })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // Same member-account wiring as the site (policies, member cookie, password rules, services).
            services.AddSingleton<DirectoryManager.Web.Services.Interfaces.ICacheService>(new FakeSiteNameCache(SiteName));
            services.AddMemberAccounts();
            services.AddScoped(sp => new AdminLoginService(
                sp.GetRequiredService<UserManager<ApplicationUser>>(),
                sp.GetRequiredService<SignInManager<ApplicationUser>>(),
                sp.GetRequiredService<IMemoryCache>(),
                this.Clock));

            this.root = services.BuildServiceProvider();
            this.scope = this.root.CreateScope();

            this.HttpContext = new DefaultHttpContext { RequestServices = this.scope.ServiceProvider };
            this.HttpContext.Request.Scheme = "https";
            this.HttpContext.Request.Host = new HostString("example.test");
            this.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = this.HttpContext;
        }

        /// <summary>Clock used only by AdminLoginService's per-step throttle (Identity uses real time).</summary>
        public MutableClock Clock { get; } = new MutableClock(DateTimeOffset.UtcNow);

        public DefaultHttpContext HttpContext { get; }

        public IServiceProvider Services => this.scope.ServiceProvider;

        public UserManager<ApplicationUser> UserManager => this.Services.GetRequiredService<UserManager<ApplicationUser>>();

        public SignInManager<ApplicationUser> SignInManager => this.Services.GetRequiredService<SignInManager<ApplicationUser>>();

        public AdminLoginService Login => this.Services.GetRequiredService<AdminLoginService>();

        public MemberAccountService Members => this.Services.GetRequiredService<MemberAccountService>();

        public ApplicationDbContext Db => this.Services.GetRequiredService<ApplicationDbContext>();

        /// <summary>RFC 6238 code (SHA-1, 6 digits, 30 s) for a base32 key, offset by whole steps from now.</summary>
        public static string Totp(string base32Key, int stepOffset = 0)
        {
            var key = FromBase32(base32Key);
            long step = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30) + stepOffset;
            var msg = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(msg);
            }

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(msg);
            int offset = hash[^1] & 0x0F;
            int binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
            return (binary % 1_000_000).ToString("D6");
        }

        /// <summary>
        /// Creates an ADMIN account (Admin role), like the accounts that existed before member
        /// sign-ups (the AddMemberAccounts migration gives all of those the role).
        /// </summary>
        public async Task<ApplicationUser> CreateUserAsync(string userName = "admin")
        {
            var user = new ApplicationUser { UserName = userName, Email = $"{userName}@example.test", CreateDate = DateTime.UtcNow };
            var result = await this.UserManager.CreateAsync(user, Password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
            await this.EnsureAdminRoleAsync();
            Assert.True((await this.UserManager.AddToRoleAsync(user, AuthRoles.Admin)).Succeeded);
            return user;
        }

        /// <summary>Creates a member account the way the sign-up page does (no email, no role).</summary>
        public async Task<ApplicationUser> CreateMemberAsync(string userName = "quiet_otter_1234", string password = MemberPassword)
        {
            await this.EnsureAdminRoleAsync();
            var result = await this.Members.RegisterAsync(userName, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Key + ": " + e.Value)));
            return result.User!;
        }

        public async Task EnsureAdminRoleAsync()
        {
            var roles = this.Services.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(AuthRoles.Admin))
            {
                Assert.True((await roles.CreateAsync(new IdentityRole(AuthRoles.Admin) { Id = AuthRoles.AdminRoleId })).Succeeded);
            }
        }

        /// <summary>Turns 2FA on the way the admin page does and returns (base32 key, recovery codes).</summary>
        public async Task<(string Key, List<string> RecoveryCodes)> EnableTwoFactorAsync(ApplicationUser user)
        {
            await this.UserManager.ResetAuthenticatorKeyAsync(user);
            var key = await this.UserManager.GetAuthenticatorKeyAsync(user);
            Assert.False(string.IsNullOrEmpty(key));
            await this.UserManager.SetTwoFactorEnabledAsync(user, true);
            var codes = (await this.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))!.ToList();
            return (key!, codes);
        }

        public async Task LockOutAsync(ApplicationUser user)
        {
            for (int i = 0; i < 5; i++)
            {
                await this.Login.CheckAsync(user.UserName!, "wrong-password", null);
            }

            Assert.True(await this.UserManager.IsLockedOutAsync(user));
        }

        /// <summary>Signs the HttpContext's principal in as this user (for [Authorize] controller tests).</summary>
        public async Task SignInPrincipalAsync(ApplicationUser user)
        {
            var factory = this.Services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
            this.HttpContext.User = await factory.CreateAsync(user);
        }

        public void Dispose()
        {
            this.scope.Dispose();
            this.root.Dispose();
        }

        private sealed class FakeSiteNameCache : DirectoryManager.Web.Services.Interfaces.ICacheService
        {
            private readonly string siteName;

            public FakeSiteNameCache(string siteName) => this.siteName = siteName;

            public void ClearSnippetCache(DirectoryManager.Data.Enums.SiteConfigSetting snippetType)
            {
            }

            public Task<string> GetSnippetAsync(DirectoryManager.Data.Enums.SiteConfigSetting snippetType) =>
                Task.FromResult(snippetType == DirectoryManager.Data.Enums.SiteConfigSetting.SiteName ? this.siteName : string.Empty);
        }

        private static byte[] FromBase32(string input)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            input = input.TrimEnd('=').ToUpperInvariant();
            var output = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (var c in input)
            {
                int v = alphabet.IndexOf(c);
                if (v < 0)
                {
                    continue;
                }

                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    output.Add((byte)(buffer >> (bits - 8)));
                    bits -= 8;
                }
            }

            return output.ToArray();
        }
    }
}
