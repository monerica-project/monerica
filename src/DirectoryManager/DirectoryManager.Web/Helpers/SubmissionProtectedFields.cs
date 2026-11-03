using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Fields of an existing listing that a public "suggest an edit" must never change, and the
    /// changes an admin must look at twice before approving. A listing's PGP key is its owner's
    /// identity (owner replies signed with it publish automatically), and its status (Verified,
    /// Scam, ...) is an admin decision, so public edits keep both as they are.
    /// </summary>
    public static class SubmissionProtectedFields
    {
        /// <summary>Keep the listing's current status and (if it has one) its PGP key.</summary>
        public static void KeepProtected(Submission submission, DirectoryEntry entry)
        {
            submission.DirectoryStatus = entry.DirectoryStatus;
            if (!string.IsNullOrWhiteSpace(entry.PgpKey))
            {
                submission.PgpKey = entry.PgpKey;
            }
        }

        /// <summary>Human-readable list of sensitive changes this submission would make to the listing.</summary>
        public static List<string> SensitiveChanges(DirectoryEntry entry, Submission submission)
        {
            var changes = new List<string>();

            if (!SameUrl(entry.Link, submission.Link))
            {
                changes.Add($"Main link changes from {entry.Link} to {submission.Link}");
            }

            if (!string.Equals((entry.Email ?? string.Empty).Trim(), (submission.Email ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            {
                changes.Add("Contact email changes");
            }

            var oldKey = NormalizeKey(entry.PgpKey);
            var newKey = NormalizeKey(submission.PgpKey);
            if (oldKey != newKey)
            {
                changes.Add(oldKey.Length == 0 ? "PGP key is added"
                    : newKey.Length == 0 ? "PGP key is REMOVED"
                    : "PGP key is REPLACED with a different key (the owner's identity)");
            }

            if (submission.DirectoryStatus is DirectoryStatus proposed
                && proposed != DirectoryStatus.Unknown
                && proposed != entry.DirectoryStatus)
            {
                changes.Add($"Status changes from {entry.DirectoryStatus} to {proposed}");
            }

            return changes;
        }

        private static bool SameUrl(string? a, string? b)
        {
            static string N(string? u) => (u ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
            return N(a) == N(b);
        }

        private static string NormalizeKey(string? key) =>
            string.Join("\n", (key ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
    }
}
