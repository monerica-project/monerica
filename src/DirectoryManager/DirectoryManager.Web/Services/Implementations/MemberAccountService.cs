using System.Security.Claims;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Member accounts: sign-up (username + password only, no email), sign-in checks, password
    /// change, self-deletion and admin disable. Admin accounts can never be used here: member
    /// sign-in treats an admin username as unknown (admins sign in on the admin login with 2FA),
    /// and a member cookie for an account that is (or becomes) an admin is rejected.
    /// No IP address is used or stored anywhere in this class.
    /// </summary>
    public class MemberAccountService
    {
        public const string DisabledReviewReason = "Hidden: the member account was disabled.";

        private static readonly Lazy<string> DummyHash = new (() =>
            new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly ApplicationDbContext db;
        private readonly ICacheService cacheService;
        private readonly MemberSignupThrottle throttle;
        private readonly IMemoryCache? memoryCache;

        public MemberAccountService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            ICacheService cacheService,
            MemberSignupThrottle throttle,
            IMemoryCache? memoryCache = null)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.db = db;
            this.cacheService = cacheService;
            this.throttle = throttle;
            this.memoryCache = memoryCache;
        }

        public UserManager<ApplicationUser> Users => this.userManager;

        /// <summary>A live member account: not deleted, not disabled.</summary>
        public static bool IsActive(ApplicationUser user) => user.DeletedUtc == null && user.DisabledUtc == null;

        public Task<bool> IsAdminAsync(ApplicationUser user) => this.userManager.IsInRoleAsync(user, AuthRoles.Admin);

        /// <summary>Names a member may not take or imitate: the site name and every admin username.</summary>
        public async Task<List<string?>> ReservedNamesAsync()
        {
            var names = new List<string?>();
            try
            {
                names.Add(await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName));
            }
            catch (Exception)
            {
                // No site name available (e.g. snippet table unavailable): the fixed list still applies.
            }

            var admins = await this.userManager.GetUsersInRoleAsync(AuthRoles.Admin);
            names.AddRange(admins.Select(a => a.UserName));
            return names;
        }

        public async Task<MemberRegisterResult> RegisterAsync(string? username, string? password)
        {
            var result = new MemberRegisterResult();
            var name = (username ?? string.Empty).Trim();

            var usernameError = MemberAccountRules.ValidateUsername(name, await this.ReservedNamesAsync());
            if (usernameError != null)
            {
                result.Errors["Username"] = usernameError;
            }

            var passwordError = MemberAccountRules.ValidatePassword(password, name);
            if (passwordError != null)
            {
                result.Errors["Password"] = passwordError;
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            // Case-insensitive: Identity looks names up by their upper-cased normalized form.
            if (await this.userManager.FindByNameAsync(name) != null)
            {
                result.Errors["Username"] = "That username is taken. Please choose another one.";
                return result;
            }

            if (!this.throttle.TryReserve())
            {
                result.Errors[string.Empty] = "Too many new accounts were created in the last hour. Please try again later.";
                return result;
            }

            var user = new ApplicationUser
            {
                UserName = name,
                Email = null,
                EmailConfirmed = false,
                CreateDate = DateTime.UtcNow,
                LockoutEnabled = true,
            };

            // Identity hashes the password (PBKDF2); it is never stored or logged in plain text.
            var created = await this.userManager.CreateAsync(user, password!);
            if (!created.Succeeded)
            {
                foreach (var e in created.Errors)
                {
                    var field = e.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase) ? "Username"
                        : e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase) ? "Password"
                        : string.Empty;
                    result.Errors[field] = e.Code == "DuplicateUserName" ? "That username is taken. Please choose another one." : e.Description;
                }

                return result;
            }

            result.User = user;
            return result;
        }

        /// <summary>
        /// Checks member credentials (with Identity's failed-attempt lockout). Returns null for any
        /// failure — unknown name, wrong password, locked, disabled, deleted, or an ADMIN account —
        /// so the caller shows one generic message. An admin's password is never checked here, so
        /// this form can't be used to test admin passwords without the admin login's 2FA.
        /// </summary>
        public async Task<ApplicationUser?> CheckSignInAsync(string? username, string? password)
        {
            var name = (username ?? string.Empty).Trim();
            var user = name.Length == 0 ? null : await this.userManager.FindByNameAsync(name);
            if (user == null || !IsActive(user) || await this.IsAdminAsync(user))
            {
                this.BurnHashTime(password);
                return null;
            }

            var check = await this.signInManager.CheckPasswordSignInAsync(user, password ?? string.Empty, lockoutOnFailure: true);
            if (!check.Succeeded)
            {
                if (check.IsLockedOut)
                {
                    this.BurnHashTime(password);
                }

                return null;
            }

            return user;
        }

        /// <summary>Resolves the account behind a member cookie, or null when it must not be used.</summary>
        public async Task<ApplicationUser?> FindValidMemberAsync(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true || !principal.HasClaim(c => c.Type == MemberAuth.MemberClaim))
            {
                return null;
            }

            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = principal.FindFirstValue(this.userManager.Options.ClaimsIdentity.SecurityStampClaimType);
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(stamp))
            {
                return null;
            }

            var user = await this.userManager.FindByIdAsync(id);
            if (user == null
                || !IsActive(user)
                || !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal)
                || await this.IsAdminAsync(user))
            {
                return null;
            }

            return user;
        }

        public ClaimsPrincipal CreatePrincipal(ApplicationUser user) =>
            DirectoryManager.Web.Extensions.MemberIdentitySetup.CreateMemberPrincipal(user, this.userManager.Options);

        public async Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string? currentPassword, string? newPassword)
        {
            var error = MemberAccountRules.ValidatePassword(newPassword, user.UserName);
            if (error != null)
            {
                return IdentityResult.Failed(new IdentityError { Code = "MemberPassword", Description = error });
            }

            // The current password is checked with the same lockout as sign-in: a stolen session
            // must not be able to guess the password here without limit.
            var check = await this.signInManager.CheckPasswordSignInAsync(user, currentPassword ?? string.Empty, lockoutOnFailure: true);
            if (!check.Succeeded)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordMismatch",
                    Description = check.IsLockedOut ? "Too many wrong passwords. Please wait a few minutes and try again." : "Incorrect password.",
                });
            }

            // ChangePasswordAsync also renews the security stamp, which signs out other devices.
            return await this.userManager.ChangePasswordAsync(user, currentPassword ?? string.Empty, newPassword!);
        }

        /// <summary>
        /// "Delete my account": the row is kept so the member's reviews keep an author, but
        /// everything personal goes — the username is replaced (and freed), the password hash is
        /// removed, the account is locked forever, and the username snapshot on their reviews and
        /// replies is cleared so they show as "deleted member". Returns false when the password
        /// is wrong or the account is an admin.
        /// </summary>
        public async Task<bool> DeleteAccountAsync(ApplicationUser user, string? password, CancellationToken ct = default)
        {
            if (await this.IsAdminAsync(user)
                || !(await this.signInManager.CheckPasswordSignInAsync(user, password ?? string.Empty, lockoutOnFailure: true)).Succeeded)
            {
                return false;
            }

            await this.ClearUsernameOnPostsAsync(user.Id, ct);

            var now = DateTime.UtcNow;
            user.DeletedUtc = now;
            user.UpdateDate = now;
            user.UserName = "deleted-" + Guid.NewGuid().ToString("N");
            user.Email = null;
            user.EmailConfirmed = false;
            user.PasswordHash = null;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.TwoFactorEnabled = false;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            // A linked PGP key is released (it can be linked again, to a new account). When the
            // account was blocked from posting, the block moves onto the key first, so deleting
            // the account is not a way to post again with the same key.
            var releasedKey = PgpFingerprintTools.Normalize(user.PgpFingerprint);
            if (user.PostingBlockedUtc != null && releasedKey.Length > 0
                && !await this.db.BlockedReviewerKeys.AnyAsync(b => b.Fingerprint == releasedKey && b.UnblockedUtc == null, ct))
            {
                var keyBlock = await this.db.BlockedReviewerKeys.FirstOrDefaultAsync(b => b.Fingerprint == releasedKey, ct);
                if (keyBlock == null)
                {
                    keyBlock = new DirectoryManager.Data.Models.Reviews.BlockedReviewerKey { Fingerprint = releasedKey };
                    this.db.BlockedReviewerKeys.Add(keyBlock);
                }

                keyBlock.Reason = "Carried over from a blocked member account that was deleted by its owner.";
                keyBlock.BlockedUtc = now;
                keyBlock.BlockedBy = user.PostingBlockedBy;
                keyBlock.UnblockedUtc = null;
                keyBlock.UnblockedBy = null;
                await this.db.SaveChangesAsync(ct);
            }

            user.PgpFingerprint = null;
            user.PgpLinkedUtc = null;
            await this.userManager.UpdateNormalizedUserNameAsync(user);
            await this.userManager.UpdateSecurityStampAsync(user); // also saves the changes above
            if (this.memoryCache != null)
            {
                MemberKeyDirectory.Invalidate(this.memoryCache);
            }

            return true;
        }

        /// <summary>
        /// Admin: disable (ban) or re-enable a member. Disabling signs the member out at once.
        /// With <paramref name="hideReviews"/>, every review and reply of theirs that is published or
        /// waiting is set to Rejected (re-enabling does not bring them back; approve them again
        /// one by one if wanted). Returns the ids of reviews whose public page changed. Never
        /// touches admin accounts.
        /// </summary>
        public async Task<(bool Ok, List<int> ChangedReviewIds)> SetDisabledAsync(string userId, bool disabled, bool hideReviews, CancellationToken ct = default)
        {
            var changed = new List<int>();
            var user = await this.userManager.FindByIdAsync(userId);
            if (user == null || user.DeletedUtc != null || await this.IsAdminAsync(user))
            {
                return (false, changed);
            }

            user.DisabledUtc = disabled ? DateTime.UtcNow : null;
            user.UpdateDate = DateTime.UtcNow;
            await this.userManager.UpdateSecurityStampAsync(user);

            if (disabled && hideReviews)
            {
                var now = DateTime.UtcNow;
                var reviews = await this.db.DirectoryEntryReviews
                    .Where(r => r.AuthorUserId == userId
                                && (r.ModerationStatus == ReviewModerationStatus.Approved
                                    || r.ModerationStatus == ReviewModerationStatus.Pending
                                    || r.ModerationStatus == ReviewModerationStatus.Flagged))
                    .ToListAsync(ct);
                foreach (var r in reviews)
                {
                    if (r.ModerationStatus == ReviewModerationStatus.Approved)
                    {
                        changed.Add(r.DirectoryEntryReviewId);
                    }

                    r.ModerationStatus = ReviewModerationStatus.Rejected;
                    r.RejectionReason = DisabledReviewReason;
                    r.UpdateDate = now;
                }

                var replies = await this.db.DirectoryEntryReviewComments
                    .Where(c => c.AuthorUserId == userId
                                && (c.ModerationStatus == ReviewModerationStatus.Approved
                                    || c.ModerationStatus == ReviewModerationStatus.Pending
                                    || c.ModerationStatus == ReviewModerationStatus.Flagged))
                    .ToListAsync(ct);
                foreach (var c in replies)
                {
                    if (c.ModerationStatus == ReviewModerationStatus.Approved)
                    {
                        changed.Add(c.DirectoryEntryReviewId);
                    }

                    c.ModerationStatus = ReviewModerationStatus.Rejected;
                    c.RejectionReason = DisabledReviewReason;
                    c.UpdateDate = now;
                }

                await this.db.SaveChangesAsync(ct);
            }

            return (true, changed.Distinct().ToList());
        }

        private async Task ClearUsernameOnPostsAsync(string userId, CancellationToken ct)
        {
            var reviews = await this.db.DirectoryEntryReviews.Where(r => r.AuthorUserId == userId).ToListAsync(ct);
            reviews.ForEach(r => r.AuthorUsername = null);
            var replies = await this.db.DirectoryEntryReviewComments.Where(c => c.AuthorUserId == userId).ToListAsync(ct);
            replies.ForEach(c => c.AuthorUsername = null);
            await this.db.SaveChangesAsync(ct);
        }

        private void BurnHashTime(string? password) =>
            this.userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyHash.Value, password ?? string.Empty);
    }
}
