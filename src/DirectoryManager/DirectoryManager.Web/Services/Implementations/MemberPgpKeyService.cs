using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Links a PGP key to a member account after the member proves control of it, the same way the
    /// no-account PGP review flow does: the key block is parsed with the same caps, the identity
    /// fingerprint and the challenge recipient are the SAME key (<see cref="IPgpService.GetFingerprint"/>
    /// / <see cref="IPgpService.EncryptTo"/>, both via <c>PgpCapabilities.SelectChallengeKey</c>), and the
    /// one-time code uses the same alphabet, length, attempt limit and constant-time compare.
    ///
    /// <para>State: nothing before sign-in (every call needs a signed-in member). While a link is
    /// pending there is ONE small in-memory entry per member (fingerprint, code, ciphertext, expiry,
    /// attempts) bound to the member id, their security stamp and the key; starting again replaces it.
    /// New challenges per member per hour are capped. No IP address is used or stored.</para>
    ///
    /// <para>A linked key is a display fact only ("this account proved it holds key X"). It never
    /// grants listing-owner or moderator powers: member posts keep their synthetic "usr:"
    /// AuthorFingerprint and stay Pending for moderation. The directory's own moderator key can't
    /// be linked at all, and a key can be linked to only one account.</para>
    /// </summary>
    public sealed class MemberPgpKeyService
    {
        public const int ChallengeMinutes = 15;
        public const int MaxChallengesPerHour = 10;

        public const string WrongPasswordMessage = "That password is not correct.";
        public const string InvalidKeyMessage = "Invalid PGP public key.";
        public const string NoEncryptionKeyMessage =
            "This PGP key can't be used: it has no usable encryption key (it is sign-only, or its encryption " +
            "subkey has expired or been revoked). We verify you by encrypting a one-time code to your key, so " +
            "please use a key with a current encryption subkey.";

        public const string ModeratorKeyMessage = "This key belongs to the directory itself and can't be linked to a member account.";
        public const string LinkedElsewhereMessage = "This PGP key is already linked to another member account. A key can be linked to only one account.";
        public const string AlreadyLinkedMessage = "This key is already linked to your account.";
        public const string ExpiredMessage = "The verification code expired or was already used. Please paste your key again to get a new code.";
        public const string OtherKeyMessage = "This code was issued for a different key. Please paste your key again to get a new code.";
        public const string WrongCodeMessage = "That code doesn't match. Decrypt the message again and enter the code exactly as shown (dashes/spaces don't matter).";
        public const string TooManyAttemptsMessage = "Too many attempts. Please paste your key again to get a new code.";
        public const string TooManyChallengesMessage = "Too many verification codes were requested for this account in the last hour. Please try again later.";

        private const int MaxRepublishPosts = 500;

        private readonly ApplicationDbContext db;
        private readonly MemberAccountService accounts;
        private readonly IPgpService pgp;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;
        private readonly TimeProvider clock;

        public MemberPgpKeyService(
            ApplicationDbContext db,
            MemberAccountService accounts,
            IPgpService pgp,
            ICacheService cacheService,
            IMemoryCache cache,
            TimeProvider? clock = null)
        {
            this.db = db;
            this.accounts = accounts;
            this.pgp = pgp;
            this.cacheService = cacheService;
            this.cache = cache;
            this.clock = clock ?? TimeProvider.System;
        }

        private DateTime NowUtc => this.clock.GetUtcNow().UtcDateTime;

        /// <summary>The pending (unexpired) link for this member, or null.</summary>
        public PendingKeyLink? GetPending(ApplicationUser member)
        {
            if (!this.cache.TryGetValue(PendingKey(member.Id), out PendingKeyLink? p) || p == null)
            {
                return null;
            }

            if (p.ExpiresUtc <= this.NowUtc || !string.Equals(p.SecurityStamp, member.SecurityStamp, StringComparison.Ordinal))
            {
                this.cache.Remove(PendingKey(member.Id));
                return null;
            }

            return p;
        }

        public void CancelPending(ApplicationUser member) => this.cache.Remove(PendingKey(member.Id));

        /// <summary>
        /// Step 1 (add or replace): checks the password and the key, then encrypts a fresh one-time
        /// code to the key and stores it as this member's only pending link. Nothing is linked yet.
        /// </summary>
        public async Task<KeyLinkResult> StartAsync(ApplicationUser member, string? password, string? armoredKey, CancellationToken ct = default)
        {
            if (!MemberAccountService.IsActive(member))
            {
                return KeyLinkResult.Fail(string.Empty, ExpiredMessage);
            }

            if (!await this.PasswordOkAsync(member, password))
            {
                return KeyLinkResult.Fail("password", WrongPasswordMessage);
            }

            // An account blocked from posting can't link (or replace) a key.
            if (await this.PostingBlockedAsync(member, ct))
            {
                return KeyLinkResult.Fail(string.Empty, ReviewerBlocks.MemberBlockedMessage);
            }

            // Same size cap and parser as the review flow's submit-key step.
            var armored = armoredKey ?? string.Empty;
            var fp = armored.Length == 0 || armored.Length > IntegerConstants.MaxPgpArmoredChars
                ? null
                : this.pgp.GetFingerprint(armored);
            fp = PgpFingerprintTools.Normalize(fp);
            if (fp.Length == 0)
            {
                return KeyLinkResult.Fail("pgpArmored", InvalidKeyMessage);
            }

            // Refuse the directory's own key: compare every key in the pasted ring (primary and
            // subkeys) and the identity with every key of the moderator's ring.
            var pasted = PgpFingerprintTools.GetAllFingerprints(armored);
            pasted.Add(fp);
            if (await this.IsModeratorKeyAsync(pasted))
            {
                return KeyLinkResult.Fail("pgpArmored", ModeratorKeyMessage);
            }

            if (await ReviewerBlocks.IsKeyBlockedAsync(this.db, fp, ct))
            {
                return KeyLinkResult.Fail("pgpArmored", ReviewerBlocks.KeyNotLinkableMessage);
            }

            if (string.Equals(member.PgpFingerprint, fp, StringComparison.Ordinal))
            {
                return KeyLinkResult.Fail("pgpArmored", AlreadyLinkedMessage);
            }

            if (await this.LinkedToOtherAccountAsync(member.Id, fp, ct))
            {
                return KeyLinkResult.Fail("pgpArmored", LinkedElsewhereMessage);
            }

            if (!this.TryTakeChallengeSlot(member.Id))
            {
                return KeyLinkResult.Fail(string.Empty, TooManyChallengesMessage);
            }

            var code = DirectoryEntryReviewsController.GenerateChallengeCodeNormalized(IntegerConstants.ChallengeLength);
            string cipher;
            try
            {
                cipher = this.pgp.EncryptTo(armored, DirectoryEntryReviewsController.FormatChallengeCodeForHumans(code));
            }
            catch (Exception)
            {
                return KeyLinkResult.Fail("pgpArmored", NoEncryptionKeyMessage);
            }

            var pending = new PendingKeyLink
            {
                UserId = member.Id,
                SecurityStamp = member.SecurityStamp,
                Fingerprint = fp,
                Code = code,
                Ciphertext = cipher,
                ExpiresUtc = this.NowUtc.AddMinutes(ChallengeMinutes),
            };

            // One pending link per member: a new key replaces (and invalidates) the previous code.
            this.cache.Set(PendingKey(member.Id), pending, TimeSpan.FromMinutes(ChallengeMinutes));
            return KeyLinkResult.Ok(pending);
        }

        /// <summary>
        /// Step 2: the decrypted code. <paramref name="fingerprint"/> is the key the member believes
        /// they are confirming (hidden form field); it must be the key the pending code was encrypted
        /// to. On success the key is linked (replacing any previous key) and the code is used up.
        /// </summary>
        public async Task<KeyLinkResult> ConfirmAsync(ApplicationUser member, string? fingerprint, string? code, CancellationToken ct = default)
        {
            var pending = this.GetPending(member);
            if (pending == null || !MemberAccountService.IsActive(member) || pending.UserId != member.Id)
            {
                return KeyLinkResult.Fail(string.Empty, ExpiredMessage);
            }

            if (!string.Equals(PgpFingerprintTools.Normalize(fingerprint), pending.Fingerprint, StringComparison.Ordinal))
            {
                this.CancelPending(member);
                return KeyLinkResult.Fail(string.Empty, OtherKeyMessage);
            }

            var submitted = DirectoryEntryReviewsController.NormalizeSubmittedCode(code);
            bool ok;
            lock (pending.Sync)
            {
                if (pending.Used)
                {
                    return KeyLinkResult.Fail(string.Empty, ExpiredMessage);
                }

                pending.Attempts++;
                if (pending.Attempts > IntegerConstants.MaxVerifyAttempts)
                {
                    pending.Used = true;
                    this.CancelPending(member);
                    return KeyLinkResult.Fail(string.Empty, TooManyAttemptsMessage);
                }

                ok = DirectoryEntryReviewsController.CodesMatchConstantTime(submitted, pending.Code);
                if (ok)
                {
                    // One-time: the code can never be used again, even if saving fails below.
                    pending.Used = true;
                }
            }

            if (!ok)
            {
                return KeyLinkResult.Fail("code", WrongCodeMessage, pending);
            }

            this.CancelPending(member);

            // Re-check at link time (the moderator key or another account may have changed meanwhile).
            if (await this.IsModeratorKeyAsync(new HashSet<string> { pending.Fingerprint }))
            {
                return KeyLinkResult.Fail(string.Empty, ModeratorKeyMessage);
            }

            if (await this.LinkedToOtherAccountAsync(member.Id, pending.Fingerprint, ct))
            {
                return KeyLinkResult.Fail(string.Empty, LinkedElsewhereMessage);
            }

            // A block set while the code was out still applies.
            if (await this.PostingBlockedAsync(member, ct))
            {
                return KeyLinkResult.Fail(string.Empty, ReviewerBlocks.MemberBlockedMessage);
            }

            if (await ReviewerBlocks.IsKeyBlockedAsync(this.db, pending.Fingerprint, ct))
            {
                return KeyLinkResult.Fail(string.Empty, ReviewerBlocks.KeyNotLinkableMessage);
            }

            var now = this.NowUtc;
            member.PgpFingerprint = pending.Fingerprint;
            member.PgpLinkedUtc = now;
            member.UpdateDate = now;
            try
            {
                var saved = await this.accounts.Users.UpdateAsync(member);
                if (!saved.Succeeded)
                {
                    return KeyLinkResult.Fail(string.Empty, LinkedElsewhereMessage);
                }
            }
            catch (DbUpdateException)
            {
                // Unique index: another account linked the same key at the same moment.
                return KeyLinkResult.Fail(string.Empty, LinkedElsewhereMessage);
            }

            MemberKeyDirectory.Invalidate(this.cache);
            return KeyLinkResult.Ok(null);
        }

        /// <summary>Unlinks the key (password required). False when the password is wrong or no key is linked.</summary>
        public async Task<KeyLinkResult> RemoveAsync(ApplicationUser member, string? password, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(member.PgpFingerprint))
            {
                return KeyLinkResult.Fail(string.Empty, "There is no PGP key linked to this account.");
            }

            if (!await this.PasswordOkAsync(member, password))
            {
                return KeyLinkResult.Fail("password", WrongPasswordMessage);
            }

            member.PgpFingerprint = null;
            member.PgpLinkedUtc = null;
            member.UpdateDate = this.NowUtc;
            await this.accounts.Users.UpdateAsync(member);
            this.CancelPending(member);
            MemberKeyDirectory.Invalidate(this.cache);
            return KeyLinkResult.Ok(null);
        }

        /// <summary>Published review ids and reply ids whose byline changes when this member's key changes.</summary>
        public async Task<(List<int> ReviewIds, List<int> ReplyIds)> PublishedPostsAsync(string userId, CancellationToken ct = default)
        {
            var reviews = await this.db.DirectoryEntryReviews.AsNoTracking()
                .Where(r => r.AuthorUserId == userId && r.ModerationStatus == ReviewModerationStatus.Approved)
                .OrderByDescending(r => r.DirectoryEntryReviewId)
                .Select(r => r.DirectoryEntryReviewId)
                .Take(MaxRepublishPosts)
                .ToListAsync(ct);
            var replies = await this.db.DirectoryEntryReviewComments.AsNoTracking()
                .Where(c => c.AuthorUserId == userId && c.ModerationStatus == ReviewModerationStatus.Approved)
                .OrderByDescending(c => c.DirectoryEntryReviewCommentId)
                .Select(c => c.DirectoryEntryReviewCommentId)
                .Take(MaxRepublishPosts)
                .ToListAsync(ct);
            return (reviews, replies);
        }

        private static string PendingKey(string userId) => "member-pgp-link:" + userId;

        private static string RateKey(string userId) => "member-pgp-link-rate:" + userId;

        private async Task<bool> PasswordOkAsync(ApplicationUser member, string? password)
        {
            // The member sign-in check: failed attempts count toward the account lockout.
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(member.UserName))
            {
                return false;
            }

            var checkedUser = await this.accounts.CheckSignInAsync(member.UserName, password);
            return checkedUser != null && checkedUser.Id == member.Id;
        }

        private async Task<bool> IsModeratorKeyAsync(HashSet<string> candidateFingerprints)
        {
            string? siteKey;
            try
            {
                siteKey = await this.cacheService.GetSnippetAsync(SiteConfigSetting.PgpKey);
            }
            catch (Exception)
            {
                siteKey = null;
            }

            if (string.IsNullOrWhiteSpace(siteKey))
            {
                return false;
            }

            var siteFps = PgpFingerprintTools.GetAllFingerprints(siteKey);
            return candidateFingerprints.Any(c => siteFps.Any(s => PgpFingerprintTools.Matches(c, s)));
        }

        private Task<bool> LinkedToOtherAccountAsync(string userId, string fingerprint, CancellationToken ct) =>
            this.db.Users.AsNoTracking().AnyAsync(u => u.PgpFingerprint == fingerprint && u.Id != userId, ct);

        private async Task<bool> PostingBlockedAsync(ApplicationUser member, CancellationToken ct) =>
            member.PostingBlockedUtc != null
            || await this.db.Users.AsNoTracking().AnyAsync(u => u.Id == member.Id && u.PostingBlockedUtc != null, ct);

        private bool TryTakeChallengeSlot(string userId)
        {
            var now = this.NowUtc;
            if (!this.cache.TryGetValue(RateKey(userId), out ChallengeWindow? window) || window == null)
            {
                window = new ChallengeWindow { StartUtc = now };
                this.cache.Set(RateKey(userId), window, TimeSpan.FromHours(1));
            }

            lock (window)
            {
                if (now - window.StartUtc >= TimeSpan.FromHours(1))
                {
                    window.StartUtc = now;
                    window.Count = 0;
                }

                if (window.Count >= MaxChallengesPerHour)
                {
                    return false;
                }

                window.Count++;
                return true;
            }
        }

        public sealed class PendingKeyLink
        {
            public string UserId { get; init; } = string.Empty;

            public string? SecurityStamp { get; init; }

            public string Fingerprint { get; init; } = string.Empty;

            public string Code { get; init; } = string.Empty;

            public string Ciphertext { get; init; } = string.Empty;

            public DateTime ExpiresUtc { get; init; }

            public int Attempts { get; set; }

            public bool Used { get; set; }

            internal object Sync { get; } = new ();
        }

        public sealed class KeyLinkResult
        {
            public bool Succeeded { get; private init; }

            public string Field { get; private init; } = string.Empty;

            public string? Error { get; private init; }

            public PendingKeyLink? Pending { get; private init; }

            public static KeyLinkResult Ok(PendingKeyLink? pending) => new () { Succeeded = true, Pending = pending };

            public static KeyLinkResult Fail(string field, string error, PendingKeyLink? pending = null) =>
                new () { Field = field, Error = error, Pending = pending };
        }

        private sealed class ChallengeWindow
        {
            public DateTime StartUtc { get; set; }

            public int Count { get; set; }
        }
    }
}
