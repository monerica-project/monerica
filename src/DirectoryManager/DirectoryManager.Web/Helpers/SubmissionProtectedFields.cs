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

            // Every place a visitor could be sent or told to make contact. A swapped onion address,
            // support handle or proof page is how a listing gets turned into a phishing page, so
            // none of these may ride through a one-click approval.
            AddIfChanged(changes, "Link 2 (onion)", entry.Link2, submission.Link2, url: true);
            AddIfChanged(changes, "Link 3 (I2P)", entry.Link3, submission.Link3, url: true);
            AddIfChanged(changes, "Proof link", entry.ProofLink, submission.ProofLink, url: true);
            AddIfChanged(changes, "Video link", entry.VideoLink, submission.VideoLink, url: true);
            AddIfChanged(changes, "Source code link", entry.SourceCodeLink, submission.SourceCodeLink, url: true);
            AddIfChanged(changes, "Messenger contact", entry.Messenger, submission.Messenger, url: false);
            AddIfChanged(changes, "Social contact", entry.Social, submission.Social, url: false);

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

        private static void AddIfChanged(List<string> changes, string label, string? current, string? proposed, bool url)
        {
            bool same = url
                ? SameUrl(current, proposed)
                : string.Equals((current ?? string.Empty).Trim(), (proposed ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
            if (!same)
            {
                changes.Add(string.IsNullOrWhiteSpace(current)
                    ? $"{label} is added: {proposed}"
                    : string.IsNullOrWhiteSpace(proposed) ? $"{label} is removed (was {current})"
                    : $"{label} changes from {current} to {proposed}");
            }
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
