using DirectoryManager.Data.DbContextInfo;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// One reviewer, one review per listing — across the two ways to review. A member account with
    /// a linked (proven) PGP key and the same key used directly in the no-account PGP flow are the
    /// same reviewer for this rule: whichever path wrote first, the other path can't add a second
    /// review of that listing with that key. Existing rows are never merged or rewritten.
    /// Fingerprints compare exactly (both sides come from <c>IPgpService.GetFingerprint</c>,
    /// uppercase hex); member posts' synthetic "usr:" keys never take part.
    /// </summary>
    public static class ReviewerKeyRules
    {
        public const string MemberAlreadyReviewedWithKeyMessage =
            "You already reviewed this listing with your PGP key (without the account). One review per listing: " +
            "edit or delete that review on the PGP \"My reviews\" page instead.";

        public const string KeyAlreadyReviewedViaMemberMessage =
            "This PGP key is linked to a member account that already reviewed this listing. One review per listing: " +
            "sign in to that account to edit its review instead.";

        /// <summary>True when a PGP-flow (no-account) review of the listing was written with this key.</summary>
        public static Task<bool> KeyHasPgpReviewAsync(ApplicationDbContext db, string? fingerprint, int directoryEntryId, CancellationToken ct = default)
        {
            var fp = PgpFingerprintTools.Normalize(fingerprint);
            if (fp.Length == 0)
            {
                return Task.FromResult(false);
            }

            return db.DirectoryEntryReviews.AsNoTracking().AnyAsync(
                r => r.DirectoryEntryId == directoryEntryId && r.AuthorUserId == null && r.AuthorFingerprint == fp,
                ct);
        }

        /// <summary>True when a member account that has this key linked already has a review of the listing.</summary>
        public static async Task<bool> KeyHasMemberReviewAsync(ApplicationDbContext db, string? fingerprint, int directoryEntryId, CancellationToken ct = default)
        {
            var fp = PgpFingerprintTools.Normalize(fingerprint);
            if (fp.Length == 0)
            {
                return false;
            }

            var memberId = await db.Users.AsNoTracking()
                .Where(u => u.PgpFingerprint == fp)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(ct);
            if (memberId == null)
            {
                return false;
            }

            return await db.DirectoryEntryReviews.AsNoTracking().AnyAsync(
                r => r.DirectoryEntryId == directoryEntryId && r.AuthorUserId == memberId,
                ct);
        }
    }
}
