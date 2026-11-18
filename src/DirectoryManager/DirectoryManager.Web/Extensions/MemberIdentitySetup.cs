using System.Security.Claims;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DirectoryManager.Web.Extensions
{
    /// <summary>
    /// Admin vs member separation.
    /// <list type="bullet">
    /// <item>Admins sign in at /account/login (password + optional 2FA) with Identity's application
    /// cookie and must hold the Admin role. Every admin action requires
    /// <see cref="AuthorizationPolicies.AdminOnly"/>, which reads ONLY that cookie and requires the
    /// role; it is also the default policy, so a bare [Authorize] means admin.</item>
    /// <item>Members sign in at /members/sign-in with a separate cookie scheme
    /// (<see cref="MemberAuth.Scheme"/>, cookie path /members). Their principal has no role and is
    /// never placed in HttpContext.User on admin or public pages, so every existing
    /// "signed in?" check still means "admin".</item>
    /// </list>
    /// </summary>
    public static class MemberIdentitySetup
    {
        /// <summary>Member cookies are re-checked against the database on every member request.</summary>
        public static readonly TimeSpan MemberCookieLifetime = TimeSpan.FromDays(30);

        public static void ConfigureAuthorization(AuthorizationOptions options)
        {
            var admin = new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(AuthRoles.Admin)
                .Build();

            options.AddPolicy(AuthorizationPolicies.AdminOnly, admin);
            options.DefaultPolicy = admin;

            options.AddPolicy(AuthorizationPolicies.Member, p => p
                .AddAuthenticationSchemes(MemberAuth.Scheme)
                .RequireAuthenticatedUser()
                .RequireClaim(MemberAuth.MemberClaim));
        }

        public static IServiceCollection AddMemberAccounts(this IServiceCollection services)
        {
            services.AddAuthorization(ConfigureAuthorization);

            services.AddAuthentication()
                .AddCookie(MemberAuth.Scheme, options =>
                {
                    options.Cookie.Name = MemberAuth.CookieName;
                    options.Cookie.Path = "/members";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;

                    // SameAsRequest: the .onion mirror is plain HTTP (same reason as the other cookies).
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.ExpireTimeSpan = MemberCookieLifetime;
                    options.SlidingExpiration = true;
                    options.LoginPath = MemberAuth.SignInPath;
                    options.AccessDeniedPath = MemberAuth.SignInPath;
                    options.LogoutPath = "/members/sign-out";
                    options.ReturnUrlParameter = "returnUrl";
                    options.Events.OnValidatePrincipal = ValidateMemberPrincipalAsync;
                });

            // Admins keep the strict password policy; members get the member rule.
            services.RemoveAll<IPasswordValidator<ApplicationUser>>();
            services.AddScoped<IPasswordValidator<ApplicationUser>, MemberAwarePasswordValidator>();

            services.AddSingleton<MemberSignupThrottle>();
            services.AddSingleton<SubmissionDraftThrottle>();
            services.AddSingleton<SubmissionLinkCheckThrottle>();
            services.AddScoped<MemberAccountService>();
            services.AddSingleton<DirectoryManager.Web.Helpers.MemberAssignedUsername>();
            services.AddScoped<DirectoryManager.Web.Services.Interfaces.IMemberReviewService, MemberReviewService>();

            // Optional proven PGP key on a member account (display only; see MemberPgpKeyService).
            services.TryAddTransient<DirectoryManager.Web.Services.Interfaces.IPgpService, DirectoryManager.Web.Services.Interfaces.PgpService>();
            services.AddScoped<MemberPgpKeyService>();
            services.AddScoped<MemberKeyDirectory>();

            // Admin "Users" page (accounts, PGP keys, posting blocks).
            services.AddScoped<UsersAdminService>();
            return services;
        }

        /// <summary>The cookie principal for a member: id, username, security stamp, member marker. No roles.</summary>
        public static ClaimsPrincipal CreateMemberPrincipal(ApplicationUser user, IdentityOptions identityOptions)
        {
            var claims = new List<Claim>
            {
                new (ClaimTypes.NameIdentifier, user.Id),
                new (ClaimTypes.Name, user.UserName ?? string.Empty),
                new (identityOptions.ClaimsIdentity.SecurityStampClaimType, user.SecurityStamp ?? string.Empty),
                new (MemberAuth.MemberClaim, "1"),
            };

            return new ClaimsPrincipal(new ClaimsIdentity(claims, MemberAuth.Scheme, ClaimTypes.Name, ClaimTypes.Role));
        }

        /// <summary>
        /// Rejects (and clears) a member cookie whose account was deleted, disabled, had its password
        /// changed elsewhere (security stamp), or is an admin account.
        /// </summary>
        public static async Task ValidateMemberPrincipalAsync(CookieValidatePrincipalContext context)
        {
            var services = context.HttpContext.RequestServices;
            var accounts = services.GetRequiredService<MemberAccountService>();
            var user = await accounts.FindValidMemberAsync(context.Principal);
            if (user == null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(MemberAuth.Scheme);
            }
        }
    }
}
