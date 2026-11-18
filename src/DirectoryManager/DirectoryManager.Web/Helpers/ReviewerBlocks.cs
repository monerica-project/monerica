using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Admin posting blocks ("Users" admin page). Two kinds:
    /// <list type="bullet">
    /// <item>a member account blocked from posting (<see cref="ApplicationUser.PostingBlockedUtc"/>);</item>
    /// <item>a PGP key on the block list (<c>BlockedReviewerKeys</c>, active while UnblockedUtc is null).</item>
    /// </list>
    /// A blocked key can't post through the no-account PGP flow (refused at the key step and again
    /// at submit), can't be linked to a member account, and a member account that has it linked
    /// can't post. A PGP key that is linked to a member account blocked from posting is treated
    /// the same way in the PGP flow (one reviewer, two ways in; same idea as
    /// <see cref="ReviewerKeyRules"/>). Messages are neutral: they never say a moderator acted.
    /// </summary>
    public static class ReviewerBlocks
    {
        public const string KeyBlockedMessage =
            "This PGP key can't be used to post reviews or replies here.";

        public const string MemberBlockedMessage =
            "Your account can't post reviews or replies at the moment.";

        public const string KeyNotLinkableMessage =
            "This PGP key can't be linked to a member account.";

        /// <summary>RejectionReason written on posts hidden by a key block (lets unblock find them again).</summary>
        public const string KeyHiddenReason = "Hidden: the PGP key was blocked.";

        /// <summary>RejectionReason written on posts hidden by an account posting block.</summary>
        public const string AccountHiddenReason = "Hidden: the member account was blocked from posting.";

        /// <summary>True when this fingerprint is on the active block list.</summary>
        public static Task<bool> IsKeyBlockedAsync(ApplicationDbContext db, string? fingerprint, CancellationToken ct = default)
        {
            var fp = PgpFingerprintTools.Normalize(fingerprint);
            if (fp.Length == 0)
            {
                return Task.FromResult(false);
            }

            return db.BlockedReviewerKeys.AsNoTracking().AnyAsync(b => b.Fingerprint == fp && b.UnblockedUtc == null, ct);
        }

        /// <summary>
        /// PGP flow check: the key is blocked itself, or it is linked to a member account that is
        /// blocked from posting.
        /// </summary>
        public static async Task<bool> IsPgpAuthorBlockedAsync(ApplicationDbContext db, string? fingerprint, CancellationToken ct = default)
        {
            var fp = PgpFingerprintTools.Normalize(fingerprint);
            if (fp.Length == 0)
            {
                return false;
            }

            if (await IsKeyBlockedAsync(db, fp, ct))
            {
                return true;
            }

            return await db.Users.AsNoTracking().AnyAsync(u => u.PgpFingerprint == fp && u.PostingBlockedUtc != null, ct);
        }

        /// <summary>Member check: the account is blocked from posting, or its linked key is blocked.</summary>
        public static async Task<bool> IsMemberBlockedAsync(ApplicationDbContext db, ApplicationUser member, CancellationToken ct = default)
        {
            // Read the current row: the instance passed in may be older than an admin's change.
            var state = await db.Users.AsNoTracking()
                .Where(u => u.Id == member.Id)
                .Select(u => new { u.PostingBlockedUtc, u.PgpFingerprint })
                .FirstOrDefaultAsync(ct);
            if (state == null)
            {
                return member.PostingBlockedUtc != null;
            }

            return state.PostingBlockedUtc != null || await IsKeyBlockedAsync(db, state.PgpFingerprint, ct);
        }
    }
}
