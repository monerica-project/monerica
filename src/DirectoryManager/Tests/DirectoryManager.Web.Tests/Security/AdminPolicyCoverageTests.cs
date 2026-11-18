using System.Reflection;
using System.Security.Claims;
using DirectoryManager.Data.Constants;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryManager.Web.Tests.Security
{
    /// <summary>
    /// The safety net for member accounts: every action that is not public by design (see
    /// <see cref="AuthorizationCoverageTests"/>) and not on the member allow-list below must
    /// require the AdminOnly policy. A controller that forgets it fails here.
    /// </summary>
    public class AdminPolicyCoverageTests
    {
        // The ONLY actions a member account may reach. Adding to this list is a security decision:
        // it must never contain admin, moderation or listing-owner actions.
        private static readonly HashSet<string> MemberAllowList = new (StringComparer.Ordinal)
        {
            "MemberAccount.SignOutPost",
            "MemberAccount.Account",
            "MemberAccount.ChangePassword",
            "MemberAccount.ChangePasswordPost",
            "MemberAccount.Delete",
            "MemberAccount.DeletePost",
            "MemberReviews.Mine",
            "MemberReviews.Write",
            "MemberReviews.WritePost",
            "MemberReviews.Reply",
            "MemberReviews.ReplyPost",
            "MemberReviews.DeleteReview",
            "MemberReviews.DeleteReply",
            "MemberPgpKey.Index",
            "MemberPgpKey.StartPost",
            "MemberPgpKey.Verify",
            "MemberPgpKey.VerifyPost",
            "MemberPgpKey.RemovePost",
        };

        public static IEnumerable<object[]> AdminSpotChecks() => new[]
        {
            new object[] { typeof(SubmissionController), nameof(SubmissionController.Review) },
            new object[] { typeof(SubmissionController), nameof(SubmissionController.AiReviewApprove) },
            new object[] { typeof(SubmissionController), nameof(SubmissionController.AiReviewApproveAll) },
            new object[] { typeof(SubmissionController), nameof(SubmissionController.RejectPost) },
            new object[] { typeof(ReviewModerationController), nameof(ReviewModerationController.Approve) },
            new object[] { typeof(ReviewModerationController), nameof(ReviewModerationController.Pending) },
            new object[] { typeof(ReviewModerationController), nameof(ReviewModerationController.ApproveReply) },
            new object[] { typeof(TwoFactorController), nameof(TwoFactorController.Index) },
            new object[] { typeof(TwoFactorController), nameof(TwoFactorController.Disable) },
            new object[] { typeof(TwoFactorController), nameof(TwoFactorController.RegenerateRecoveryCodes) },
            new object[] { typeof(AccountController), "HomeAsync" },
            new object[] { typeof(AccountController), "ChangePasswordAsync" },
            new object[] { typeof(MembersAdminController), nameof(MembersAdminController.Disable) },
            new object[] { typeof(MembersAdminController), nameof(MembersAdminController.Enable) },
            new object[] { typeof(MembersAdminController), nameof(MembersAdminController.Index) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.Index) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.Account) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.Key) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.KeyLookup) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.BlockAccount) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.UnblockAccount) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.RemoveKey) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.BlockKey) },
            new object[] { typeof(UsersAdminController), nameof(UsersAdminController.UnblockKey) },
            new object[] { typeof(DirectoryEntryReviewsController), nameof(DirectoryEntryReviewsController.DeleteConfirmed) },
            new object[] { typeof(EmailSendLogController), nameof(EmailSendLogController.Index) },
            new object[] { typeof(EmailSendLogController), nameof(EmailSendLogController.Details) },
        };

        [Fact]
        public void EveryNonPublicNonMemberActionRequiresTheAdminPolicy()
        {
            var publicByDesign = PublicByDesign();
            var problems = new List<string>();
            var memberActions = new List<string>();

            foreach (var (controller, name, action) in Actions())
            {
                var key = $"{name}.{action.Name}";
                var attrs = AuthorizeOf(controller, action);
                bool anonymous = action.GetCustomAttribute<AllowAnonymousAttribute>(true) != null
                                 || (controller.GetCustomAttribute<AllowAnonymousAttribute>(true) != null && action.GetCustomAttribute<AuthorizeAttribute>(true) == null);

                if (attrs.Any(a => a.Policy == AuthorizationPolicies.Member))
                {
                    memberActions.Add(key);
                }

                publicByDesign.TryGetValue(name, out var allowed);
                allowed ??= Array.Empty<string>();
                if (allowed.Contains("*") || allowed.Contains(action.Name))
                {
                    continue;
                }

                if (MemberAllowList.Contains(key))
                {
                    if (anonymous || !attrs.Any(a => a.Policy == AuthorizationPolicies.Member))
                    {
                        problems.Add(key + " (member action without the Member policy)");
                    }

                    if (attrs.Any(a => a.Policy == AuthorizationPolicies.AdminOnly))
                    {
                        problems.Add(key + " (member action also marked AdminOnly)");
                    }

                    continue;
                }

                if (anonymous)
                {
                    problems.Add(key + " (AllowAnonymous but not on the public list)");
                }
                else if (!attrs.Any(a => a.Policy == AuthorizationPolicies.AdminOnly))
                {
                    problems.Add(key + " (no AdminOnly policy)");
                }
                else if (attrs.Any(a => a.Policy == AuthorizationPolicies.Member || !string.IsNullOrEmpty(a.AuthenticationSchemes)))
                {
                    problems.Add(key + " (admin action also accepts the member policy/scheme)");
                }
            }

            Assert.True(problems.Count == 0, "Admin coverage problems:\n" + string.Join("\n", problems.Distinct().OrderBy(x => x)));

            var unexpectedMember = memberActions.Where(a => !MemberAllowList.Contains(a)).Distinct().ToList();
            Assert.True(unexpectedMember.Count == 0, "Member-policy actions not on the allow-list: " + string.Join(", ", unexpectedMember));
            var stale = MemberAllowList.Where(a => !memberActions.Contains(a)).ToList();
            Assert.True(stale.Count == 0, "Allow-listed member actions that no longer exist: " + string.Join(", ", stale));
        }

        [Fact]
        public void EveryAuthorizeAttributeNamesAdminOnlyOrMember()
        {
            var bad = Actions()
                .SelectMany(x => AuthorizeOf(x.Controller, x.Action).Select(a => (x.Name, x.Action, a)))
                .Where(x => (x.a.Policy != AuthorizationPolicies.AdminOnly && x.a.Policy != AuthorizationPolicies.Member)
                            || !string.IsNullOrEmpty(x.a.Roles)
                            || !string.IsNullOrEmpty(x.a.AuthenticationSchemes))
                .Select(x => $"{x.Name}.{x.Action.Name}")
                .Distinct()
                .ToList();
            Assert.True(bad.Count == 0, "[Authorize] without the AdminOnly/Member policy: " + string.Join(", ", bad));
        }

        [Fact]
        public void PoliciesAreConfiguredSafely()
        {
            var options = new AuthorizationOptions();
            MemberIdentitySetup.ConfigureAuthorization(options);

            var admin = options.GetPolicy(AuthorizationPolicies.AdminOnly)!;
            Assert.Contains(AuthRoles.Admin, admin.Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles);
            Assert.Equal(new[] { IdentityConstants.ApplicationScheme }, admin.AuthenticationSchemes);

            // A bare [Authorize] fails closed: the default policy is the admin policy.
            Assert.Contains(options.DefaultPolicy.Requirements, r => r is RolesAuthorizationRequirement);
            Assert.Equal(new[] { IdentityConstants.ApplicationScheme }, options.DefaultPolicy.AuthenticationSchemes);

            // The member policy reads only the member cookie and never needs (or grants) a role.
            var member = options.GetPolicy(AuthorizationPolicies.Member)!;
            Assert.Equal(new[] { MemberAuth.Scheme }, member.AuthenticationSchemes);
            Assert.DoesNotContain(member.Requirements, r => r is RolesAuthorizationRequirement);
        }

        [Theory]
        [MemberData(nameof(AdminSpotChecks))]
        public async Task MemberCannotReachAdminAction_AdminCan(Type controller, string actionName)
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            var adminPrincipal = await h.Services.GetRequiredService<IUserClaimsPrincipalFactory<DirectoryManager.Data.Models.ApplicationUser>>().CreateAsync(admin);
            var memberPrincipal = h.Members.CreatePrincipal(member);
            var memberWithFakeAdminIdentity = new ClaimsPrincipal(new ClaimsIdentity(memberPrincipal.Claims, IdentityConstants.ApplicationScheme));

            var policyProvider = h.Services.GetRequiredService<IAuthorizationPolicyProvider>();
            var authz = h.Services.GetRequiredService<IAuthorizationService>();

            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == actionName)
                .ToList();
            Assert.NotEmpty(methods);

            foreach (var method in methods)
            {
                var policy = await AuthorizationPolicy.CombineAsync(policyProvider, AuthorizeOf(controller, method));
                Assert.NotNull(policy);

                // The admin policy authenticates with the admin cookie only, never the member one.
                Assert.DoesNotContain(MemberAuth.Scheme, policy!.AuthenticationSchemes);

                Assert.False((await authz.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), policy)).Succeeded, $"anonymous reached {controller.Name}.{method.Name}");
                Assert.False((await authz.AuthorizeAsync(memberPrincipal, policy)).Succeeded, $"member reached {controller.Name}.{method.Name}");
                Assert.False((await authz.AuthorizeAsync(memberWithFakeAdminIdentity, policy)).Succeeded, $"member claims reached {controller.Name}.{method.Name}");
                Assert.True((await authz.AuthorizeAsync(adminPrincipal, policy)).Succeeded, $"admin blocked from {controller.Name}.{method.Name}");
            }
        }

        [Fact]
        public async Task SignedInAccountWithoutTheAdminRole_IsNotAnAdmin()
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            var factory = h.Services.GetRequiredService<IUserClaimsPrincipalFactory<DirectoryManager.Data.Models.ApplicationUser>>();
            var authz = h.Services.GetRequiredService<IAuthorizationService>();

            Assert.True((await authz.AuthorizeAsync(await factory.CreateAsync(admin), AuthorizationPolicies.AdminOnly)).Succeeded);
            Assert.True(DirectoryManager.Web.Helpers.AdminAccess.IsAdmin(await factory.CreateAsync(admin)));

            // An Identity account that lacks the role (e.g. a member created in the same store) is not an admin.
            var member = await h.CreateMemberAsync("brave_heron_1111");
            var noRole = await factory.CreateAsync(member);
            Assert.False((await authz.AuthorizeAsync(noRole, AuthorizationPolicies.AdminOnly)).Succeeded);
            Assert.False(DirectoryManager.Web.Helpers.AdminAccess.IsAdmin(noRole));
            Assert.False(DirectoryManager.Web.Helpers.AdminAccess.IsAdmin(h.Members.CreatePrincipal(member)));
            Assert.False((await authz.AuthorizeAsync(await factory.CreateAsync(admin), AuthorizationPolicies.Member)).Succeeded);
        }

        [Fact]
        public void MigrationGivesEveryExistingAccountTheAdminRole()
        {
            var migration = new DirectoryManager.Data.Migrations.AddMemberAccounts();
            var builder = new Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            typeof(Microsoft.EntityFrameworkCore.Migrations.Migration)
                .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(migration, new object[] { builder });

            var sql = builder.Operations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(o => o.Sql).ToList();
            Assert.Contains(sql, s => s.Contains("INSERT INTO \"AspNetRoles\"", StringComparison.Ordinal) && s.Contains(AuthRoles.AdminRoleId, StringComparison.Ordinal));
            Assert.Contains(sql, s => s.Contains("INSERT INTO \"AspNetUserRoles\"", StringComparison.Ordinal)
                                      && s.Contains("FROM \"AspNetUsers\"", StringComparison.Ordinal)
                                      && s.Contains("'IdentityUserRole<string>'", StringComparison.Ordinal)
                                      && !s.Contains("WHERE u.", StringComparison.Ordinal));
        }

        private static Dictionary<string, string[]> PublicByDesign() =>
            (Dictionary<string, string[]>)typeof(AuthorizationCoverageTests)
                .GetField("PublicByDesign", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

        private static IEnumerable<(Type Controller, string Name, MethodInfo Action)> Actions()
        {
            foreach (var type in typeof(HomeController).Assembly.GetTypes().Where(t =>
                         !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t) && t.GetCustomAttribute<NonControllerAttribute>() == null))
            {
                var name = type.Name.EndsWith("Controller", StringComparison.Ordinal) ? type.Name[..^"Controller".Length] : type.Name;
                foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                             .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null))
                {
                    yield return (type, name, m);
                }
            }
        }

        private static List<AuthorizeAttribute> AuthorizeOf(Type t, MethodInfo m) =>
            t.GetCustomAttributes<AuthorizeAttribute>(true).Concat(m.GetCustomAttributes<AuthorizeAttribute>(true)).ToList();
    }
}
