using System.Text;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// How member-written reviews and replies are identified and shown.
    ///
    /// <para>A member post keeps the required AuthorFingerprint column filled with a synthetic
    /// key: "usr:" followed by the account id with every hex digit 0–f mapped to the letters
    /// g–v. That key contains NO hexadecimal characters at all, so
    /// <see cref="PgpFingerprintTools.Normalize"/> turns it into an empty string and
    /// <see cref="PgpFingerprintTools.Matches"/> is always false for it. A member post can therefore
    /// never be mistaken for the listing owner's key, the directory's moderator key, or another
    /// reviewer's PGP fingerprint by any of the existing fingerprint comparisons.</para>
    /// </summary>
    public static class MemberAuthor
    {
        public const string KeyPrefix = "usr:";

        /// <summary>Public byline label that sets member posts apart from PGP-verified ones.</summary>
        public const string PublicLabel = "account";

        /// <summary>Byline label while the account has a proven PGP key linked (shown with the key).</summary>
        public const string VerifiedLabel = "member account, PGP-verified";

        /// <summary>Hover text for <see cref="VerifiedLabel"/>: what was verified, and what was not.</summary>
        public const string VerifiedTitle =
            "This member account proved it holds this PGP key by decrypting a one-time code. Posts are not signed individually.";

        public const string DeletedMemberName = "deleted member";

        /// <summary>The synthetic AuthorFingerprint value for a member's posts (36 chars for a GUID id).</summary>
        public static string AuthorKey(string userId)
        {
            var sb = new StringBuilder(KeyPrefix.Length + 32);
            sb.Append(KeyPrefix);
            foreach (var ch in (userId ?? string.Empty).ToLowerInvariant())
            {
                if (ch >= '0' && ch <= '9')
                {
                    sb.Append((char)('g' + (ch - '0')));
                }
                else if (ch >= 'a' && ch <= 'f')
                {
                    sb.Append((char)('q' + (ch - 'a')));
                }
                else if (ch >= 'g' && ch <= 'z')
                {
                    // Not expected in a GUID; keep letters that are not hex digits as they are.
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        public static bool IsMember(string? authorUserId) => !string.IsNullOrEmpty(authorUserId);

        /// <summary>The name shown for a member post (the username, or "deleted member").</summary>
        public static string PublicName(string? authorUsername) =>
            string.IsNullOrWhiteSpace(authorUsername) ? DeletedMemberName : authorUsername.Trim();

        /// <summary>
        /// The public label for a member post. It follows the account's CURRENT key
        /// (<paramref name="linkedFingerprint"/>, null when none): once a key is removed or replaced,
        /// no post keeps showing the old key.
        /// </summary>
        public static string LabelFor(string? linkedFingerprint) =>
            string.IsNullOrWhiteSpace(linkedFingerprint) ? PublicLabel : VerifiedLabel;

        /// <summary>One-line author description for admin pages: author type plus identity.</summary>
        public static string ForAdmin(string? authorFingerprint, string? authorUserId, string? authorUsername) =>
            IsMember(authorUserId)
                ? $"Member: {PublicName(authorUsername)}"
                : $"PGP: {authorFingerprint}";

        /// <summary>Admin description including the member's linked PGP key (if any).</summary>
        public static string ForAdmin(string? authorFingerprint, string? authorUserId, string? authorUsername, string? linkedFingerprint) =>
            IsMember(authorUserId)
                ? ForAdmin(authorFingerprint, authorUserId, authorUsername) + (string.IsNullOrWhiteSpace(linkedFingerprint) ? " (no PGP key)" : $" (linked PGP key {linkedFingerprint})")
                : ForAdmin(authorFingerprint, authorUserId, authorUsername);
    }
}
