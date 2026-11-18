using DirectoryManager.Data.Constants;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Tests.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Tests.Members
{
    public class MemberAccountServiceTests
    {
        [Fact]
        public async Task SignUpNeedsOnlyAUsernameAndPassword_NoEmail_NoRole()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            Assert.Null(member.Email);
            Assert.False(await h.UserManager.IsInRoleAsync(member, AuthRoles.Admin));
            Assert.Empty(await h.UserManager.GetRolesAsync(member));

            // Stored as an Identity (PBKDF2) hash, never the plain text.
            Assert.False(string.IsNullOrEmpty(member.PasswordHash));
            Assert.DoesNotContain(IdentityHarness.MemberPassword, member.PasswordHash!);
            Assert.Equal(PasswordVerificationResult.Success, h.UserManager.PasswordHasher.VerifyHashedPassword(member, member.PasswordHash!, IdentityHarness.MemberPassword));
        }

        [Fact]
        public async Task UsernamesAreUniqueIgnoringCase()
        {
            using var h = new IdentityHarness();
            await h.CreateMemberAsync("Brave_Heron");
            var dup = await h.Members.RegisterAsync("brave_heron", IdentityHarness.MemberPassword);
            Assert.False(dup.Succeeded);
            Assert.True(dup.Errors.ContainsKey("Username"));
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("Moderator")]
        [InlineData("support")]
        [InlineData("ExampleDirectory")] // the configured site name (IdentityHarness.SiteName)
        [InlineData("OWNER")]            // the existing admin's username, any case
        [InlineData("owner_example")]
        public async Task ReservedAndAdminImitatingNamesAreRefused(string name)
        {
            using var h = new IdentityHarness();
            await h.CreateUserAsync("owner@example.com");
            var result = await h.Members.RegisterAsync(name, IdentityHarness.MemberPassword);
            Assert.False(result.Succeeded);
            Assert.True(result.Errors.ContainsKey("Username"));
            Assert.Null(await h.UserManager.FindByNameAsync(name));
        }

        [Fact]
        public async Task MemberPasswordRule_IsLighterThanTheAdminRule()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("calm_lynx_2002", "all lower case words");
            Assert.NotNull(member);

            var tooShort = await h.Members.RegisterAsync("calm_lynx_2003", "short");
            Assert.False(tooShort.Succeeded);

            // Admins keep the strict rule (12+ with upper/lower/digit/symbol).
            var admin = await h.CreateUserAsync("owner@example.com");
            var weak = await h.UserManager.ChangePasswordAsync(admin, IdentityHarness.Password, "all lower case words");
            Assert.False(weak.Succeeded);
        }

        [Fact]
        public async Task MemberSignIn_WorksForMembers_NeverForAdmins()
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            await h.CreateMemberAsync("swift_fox_7777");

            Assert.NotNull(await h.Members.CheckSignInAsync("SWIFT_FOX_7777", IdentityHarness.MemberPassword));
            Assert.Null(await h.Members.CheckSignInAsync("swift_fox_7777", "wrong password here"));
            Assert.Null(await h.Members.CheckSignInAsync("nobody_here", IdentityHarness.MemberPassword));

            // The admin's correct password is refused on the member form (no 2FA bypass, no
            // password oracle) and the admin's failed-attempt counter is untouched.
            Assert.Null(await h.Members.CheckSignInAsync("owner@example.com", IdentityHarness.Password));
            var reloaded = await h.UserManager.FindByIdAsync(admin.Id);
            Assert.Equal(0, reloaded!.AccessFailedCount);
        }

        [Fact]
        public async Task AdminLogin_RefusesMemberAccounts_AcceptsAdmins()
        {
            using var h = new IdentityHarness();
            await h.CreateUserAsync("owner@example.com");
            var member = await h.CreateMemberAsync("swift_fox_7777");

            Assert.Equal(AdminLoginOutcome.Failed, (await h.Login.CheckAsync("swift_fox_7777", IdentityHarness.MemberPassword, null)).Outcome);
            Assert.Equal(0, (await h.UserManager.FindByIdAsync(member.Id))!.AccessFailedCount);
            Assert.Equal(AdminLoginOutcome.Success, (await h.Login.CheckAsync("owner@example.com", IdentityHarness.Password, null)).Outcome);
        }

        [Fact]
        public async Task RepeatedFailuresPauseMemberSignIn()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("lock_me_9999");
            for (var i = 0; i < 5; i++)
            {
                Assert.Null(await h.Members.CheckSignInAsync("lock_me_9999", "not the password"));
            }

            Assert.True(await h.UserManager.IsLockedOutAsync(member));
            Assert.Null(await h.Members.CheckSignInAsync("lock_me_9999", IdentityHarness.MemberPassword));
        }

        [Fact]
        public async Task MemberCookieIsRejected_AfterPasswordChangeDisableOrDelete()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("brave_heron_1111");
            var principal = h.Members.CreatePrincipal(member);
            Assert.NotNull(await h.Members.FindValidMemberAsync(principal));

            // Password change renews the security stamp: old cookies stop working.
            Assert.True((await h.Members.ChangePasswordAsync(member, IdentityHarness.MemberPassword, "another long passphrase")).Succeeded);
            Assert.Null(await h.Members.FindValidMemberAsync(principal));
            principal = h.Members.CreatePrincipal(member);
            Assert.NotNull(await h.Members.FindValidMemberAsync(principal));

            Assert.True((await h.Members.SetDisabledAsync(member.Id, true, false)).Ok);
            Assert.Null(await h.Members.FindValidMemberAsync(principal));
            Assert.Null(await h.Members.FindValidMemberAsync(h.Members.CreatePrincipal(member)));
            Assert.Null(await h.Members.CheckSignInAsync("brave_heron_1111", "another long passphrase"));
        }

        [Fact]
        public async Task AMemberCookieForAnAdminAccountIsRejected()
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            Assert.Null(await h.Members.FindValidMemberAsync(h.Members.CreatePrincipal(admin)));
        }

        [Fact]
        public async Task DeleteAccount_AnonymizesAndKeepsReviewsAsDeletedMember()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("gentle_owl_3030");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            h.Db.DirectoryEntryReviews.Add(ReviewTestData.MemberReview(member, entry, ReviewModerationStatus.Approved));
            await h.Db.SaveChangesAsync();

            Assert.False(await h.Members.DeleteAccountAsync(member, "wrong password"));
            Assert.True(await h.Members.DeleteAccountAsync(member, IdentityHarness.MemberPassword));

            var gone = await h.UserManager.FindByIdAsync(member.Id);
            Assert.NotNull(gone!.DeletedUtc);
            Assert.Null(gone.PasswordHash);
            Assert.StartsWith("deleted-", gone.UserName);
            Assert.Null(await h.UserManager.FindByNameAsync("gentle_owl_3030")); // name is free again
            Assert.Null(await h.Members.CheckSignInAsync("gentle_owl_3030", IdentityHarness.MemberPassword));

            var review = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync();
            Assert.Equal(member.Id, review.AuthorUserId);
            Assert.Null(review.AuthorUsername); // shown as "deleted member"
        }

        [Fact]
        public async Task DisableWithHide_RejectsTheirPosts_AndNeverTouchesAdmins()
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            var member = await h.CreateMemberAsync("rapid_puffin_5151");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var published = ReviewTestData.MemberReview(member, entry, ReviewModerationStatus.Approved);
            h.Db.DirectoryEntryReviews.Add(published);
            await h.Db.SaveChangesAsync();

            var (ok, changed) = await h.Members.SetDisabledAsync(member.Id, true, hideReviews: true);
            Assert.True(ok);
            Assert.Contains(published.DirectoryEntryReviewId, changed);
            var review = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync();
            Assert.Equal(ReviewModerationStatus.Rejected, review.ModerationStatus);

            Assert.False((await h.Members.SetDisabledAsync(admin.Id, true, true)).Ok);
            Assert.Null((await h.UserManager.FindByIdAsync(admin.Id))!.DisabledUtc);

            Assert.True((await h.Members.SetDisabledAsync(member.Id, false, false)).Ok);
            Assert.NotNull(await h.Members.CheckSignInAsync("rapid_puffin_5151", IdentityHarness.MemberPassword));
        }

        [Fact]
        public void SignupThrottleCapsAccountsPerHour_WithoutAnyVisitorData()
        {
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            var throttle = new MemberSignupThrottle(clock);
            for (var i = 0; i < MemberSignupThrottle.MaxPerHour; i++)
            {
                Assert.True(throttle.TryReserve());
            }

            Assert.False(throttle.TryReserve());
            clock.Advance(TimeSpan.FromMinutes(61));
            Assert.True(throttle.TryReserve());
        }
    }
}
