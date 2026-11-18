using DirectoryManager.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>What the login form should do with an attempt.</summary>
    public enum AdminLoginOutcome
    {
        /// <summary>Credentials are good: sign the user in.</summary>
        Success = 0,

        /// <summary>Anything wrong (unknown user, bad password, bad/missing code, locked out). One generic message.</summary>
        Failed = 1,

        /// <summary>Identity refuses sign-in for this account (account confirmation rules). Only after full credentials passed.</summary>
        NotAllowed = 2,
    }

    /// <summary>The result of one login attempt.</summary>
    public sealed record AdminLoginResult(AdminLoginOutcome Outcome, ApplicationUser? User, bool UsedSecondFactor)
    {
        public static AdminLoginResult Failed { get; } = new (AdminLoginOutcome.Failed, null, false);
    }

    /// <summary>
    /// Checks admin credentials for the single-step login form (username + password + optional
    /// authenticator/recovery code). It never signs in; the controller does that on Success.
    ///
    /// <para>2FA off: same as Identity's password sign-in (failed-attempt counter, 15-minute lockout).</para>
    /// <para>2FA on: the password AND a valid code are required. Because a stranger now needs both,
    /// a correct password + code is accepted even while the account is locked out, so lockout can no
    /// longer be used to keep the owner out. Failures still count toward the lockout.</para>
    /// <para>Every failure looks the same to the caller (no hint whether the user exists, the account
    /// is locked, or the password was right). No IP address is used or stored.</para>
    /// </summary>
    public class AdminLoginService
    {
        /// <summary>
        /// Most authenticator codes checked per account per 30-second step. Only reached by someone who
        /// already sent the correct password, so a stranger cannot use it to block the owner.
        /// </summary>
        public const int MaxCodeChecksPerStep = 5;

        private const string AmrPassword = "pwd";
        private const string AmrMfa = "mfa";

        // Accepted TOTP codes are remembered this long so the same code can't be replayed.
        // Identity accepts codes up to two 30-second steps either side of now (~2.5 min).
        private static readonly TimeSpan UsedCodeMemory = TimeSpan.FromMinutes(5);
        private static readonly Lazy<string> DummyHash = new (() =>
            new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly IMemoryCache cache;
        private readonly TimeProvider timeProvider;

        public AdminLoginService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IMemoryCache cache,
            TimeProvider? timeProvider = null)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.cache = cache;
            this.timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>The amr claim value to put on the cookie for this result.</summary>
        public static string AuthenticationMethod(AdminLoginResult result) => result.UsedSecondFactor ? AmrMfa : AmrPassword;

        /// <summary>Removes spaces/hyphens from a typed 6-digit code; returns null when it isn't one.</summary>
        public static string? NormalizeTotp(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var digits = new string(code.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
            return digits.Length == 6 && digits.All(char.IsAsciiDigit) ? digits : null;
        }

        /// <summary>Recovery codes look like "XXXXX-XXXXX"; accept them typed with spaces or lower case.</summary>
        public static string? NormalizeRecoveryCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var cleaned = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
            return cleaned.Length is > 0 and <= 64 ? cleaned : null;
        }

        public async Task<AdminLoginResult> CheckAsync(string username, string password, string? code)
        {
            var user = await this.userManager.FindByNameAsync(username);

            // Only Admin-role accounts may use the admin login. A member account (username +
            // password sign-up) is treated exactly like an unknown user: same message, no password
            // check, and it never touches that account's failed-attempt counter.
            if (user == null || !await this.userManager.IsInRoleAsync(user, DirectoryManager.Data.Constants.AuthRoles.Admin))
            {
                this.BurnHashTime(password);
                return AdminLoginResult.Failed;
            }

            if (!user.TwoFactorEnabled)
            {
                // Unchanged path: Identity's own password check with lockoutOnFailure.
                var result = await this.signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    return new AdminLoginResult(AdminLoginOutcome.Success, user, false);
                }

                if (result.IsNotAllowed)
                {
                    return new AdminLoginResult(AdminLoginOutcome.NotAllowed, null, false);
                }

                if (result.IsLockedOut)
                {
                    // Identity returns early for a locked account; spend about the time of a real check.
                    this.BurnHashTime(password);
                }

                return AdminLoginResult.Failed;
            }

            // 2FA on: always check the password (constant cost), then the code only if the password was right.
            bool passwordOk = await this.userManager.CheckPasswordAsync(user, password);
            bool codeOk = false;
            if (passwordOk)
            {
                codeOk = await this.VerifySecondFactorAsync(user, code);
            }
            else
            {
                // Same store read as a recovery-code check, so timing doesn't hint the password was wrong.
                await this.userManager.CountRecoveryCodesAsync(user);
            }

            if (!passwordOk || !codeOk)
            {
                await this.userManager.AccessFailedAsync(user);
                return AdminLoginResult.Failed;
            }

            if (!await this.signInManager.CanSignInAsync(user))
            {
                return new AdminLoginResult(AdminLoginOutcome.NotAllowed, null, false);
            }

            // Correct password + code: the owner is in, even if strangers had tripped the lockout.
            await this.userManager.ResetAccessFailedCountAsync(user);
            if (await this.userManager.IsLockedOutAsync(user))
            {
                await this.userManager.SetLockoutEndDateAsync(user, null);
            }

            return new AdminLoginResult(AdminLoginOutcome.Success, user, true);
        }

        /// <summary>
        /// Checks an authenticator code (rate-limited per step, replay-protected) or, failing that
        /// shape, a one-time recovery code (consumed on success).
        /// </summary>
        public async Task<bool> VerifySecondFactorAsync(ApplicationUser user, string? code, bool allowRecoveryCode = true)
        {
            var totp = NormalizeTotp(code);
            if (totp != null)
            {
                return await this.VerifyTotpAsync(user, totp);
            }

            if (!allowRecoveryCode)
            {
                return false;
            }

            var recovery = NormalizeRecoveryCode(code);
            if (recovery == null)
            {
                return false;
            }

            var redeemed = await this.userManager.RedeemTwoFactorRecoveryCodeAsync(user, recovery);
            return redeemed.Succeeded;
        }

        /// <summary>Authenticator code only (no recovery code), with the same throttle and replay protection.</summary>
        public async Task<bool> VerifyTotpAsync(ApplicationUser user, string totp)
        {
            long step = this.timeProvider.GetUtcNow().ToUnixTimeSeconds() / 30;
            string countKey = $"admin-2fa-checks:{user.Id}:{step}";
            int count = this.cache.GetOrCreate(countKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return 0;
            });
            if (count >= MaxCodeChecksPerStep)
            {
                return false;
            }

            this.cache.Set(countKey, count + 1, TimeSpan.FromMinutes(2));

            string usedKey = $"admin-2fa-used:{user.Id}:{totp}";
            if (this.cache.TryGetValue(usedKey, out _))
            {
                return false;
            }

            bool ok = await this.userManager.VerifyTwoFactorTokenAsync(
                user, this.userManager.Options.Tokens.AuthenticatorTokenProvider, totp);
            if (ok)
            {
                this.cache.Set(usedKey, true, UsedCodeMemory);
            }

            return ok;
        }

        private void BurnHashTime(string password)
        {
            this.userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyHash.Value, password ?? string.Empty);
        }
    }
}
