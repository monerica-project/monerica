using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// The quick Approve / Reject decision on the admin submission review page: the list of
    /// rejection reasons, where to send the admin afterwards, and the extra confirmation a quick
    /// approve needs when it changes protected listing fields.
    /// </summary>
    public static class SubmissionDecision
    {
        public const string OtherReasonKey = "other";
        public const string SpamReasonKey = "spam";
        public const int OtherReasonMaxLength = 300;

        // Where the admin came from. Only these values are used, so "from" can never redirect off-site.
        public const string FromPending = "pending";
        public const string FromAiReview = "ai-review";
        public const string FromAll = "all";

        /// <summary>
        /// Common rejection reasons, worded after the public submission rules (also the automated
        /// reviewer's reject rules) and its pre-filter (duplicate, unreachable, spam).
        /// </summary>
        public static readonly IReadOnlyList<(string Key, string Label)> Reasons = new List<(string, string)>
        {
            ("duplicate", "Duplicate of an existing listing or submission"),
            ("no-monero", "Does not clearly show it accepts Monero (verifiable without an account)"),
            ("not-own-site", "Link is a profile or listing on another platform, not the business's own site"),
            ("not-working", "Site is down, unreachable or not fully working (parked, \"coming soon\", broken shop)"),
            ("off-topic", "Not allowed in the directory (political party, hacker group, religion, sexual orientation)"),
            ("illegal", "Illegal activity, mixer/tumbler, or marketed around evading the law (\"No AML\")"),
            ("scam", "Scam or unsafe site"),
            ("not-enough-info", "Not enough information to list it"),
            (SpamReasonKey, "Spam"),
            ("test", "Test submission"),
        };

        /// <summary>
        /// The reason text to record, or null with an error message when the choice is missing or
        /// "Other" has no text.
        /// </summary>
        public static string? ResolveReason(string? reasonKey, string? otherText, out string? error)
        {
            error = null;
            var key = (reasonKey ?? string.Empty).Trim();
            var other = (otherText ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();

            if (other.Length > OtherReasonMaxLength)
            {
                error = $"The typed reason is {other.Length} characters; keep it to {OtherReasonMaxLength}.";
                return null;
            }

            // Text typed in the box without picking "Other" (no JavaScript to pick it) still counts.
            if (string.Equals(key, OtherReasonKey, StringComparison.OrdinalIgnoreCase) || (key.Length == 0 && other.Length > 0))
            {
                if (other.Length == 0)
                {
                    error = "Type the reason in the \"Other\" box.";
                    return null;
                }

                return other;
            }

            foreach (var (k, label) in Reasons)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    // Extra detail typed alongside a listed reason is kept with it.
                    return other.Length == 0 ? label : $"{label}: {other}";
                }
            }

            error = "Choose a reason for rejecting.";
            return null;
        }

        /// <summary>
        /// The reason to pre-select from the automated pre-filter's finding (stored as e.g.
        /// "AutoReject: Spam: empty description"), or null.
        /// </summary>
        public static string? SuggestReasonKey(string? prefilterReason)
        {
            var p = prefilterReason ?? string.Empty;
            if (p.Contains("Spam:", StringComparison.OrdinalIgnoreCase))
            {
                return SpamReasonKey;
            }

            if (p.Contains("Already listed", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("already submitted", StringComparison.OrdinalIgnoreCase))
            {
                return "duplicate";
            }

            if (p.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
            {
                return "not-working";
            }

            return null;
        }

        /// <summary>Spam is recorded with the Spam status; every other reason is Rejected.</summary>
        public static SubmissionStatus StatusFor(string? reasonKey) =>
            string.Equals((reasonKey ?? string.Empty).Trim(), SpamReasonKey, StringComparison.OrdinalIgnoreCase)
                ? SubmissionStatus.Spam
                : SubmissionStatus.Rejected;

        /// <summary>Known "from" value (lowercase), or null.</summary>
        public static string? NormalizeFrom(string? from)
        {
            var f = (from ?? string.Empty).Trim().ToLowerInvariant();
            return f is FromPending or FromAiReview or FromAll ? f : null;
        }

        /// <summary>Local path of the list the admin came from (never anything else).</summary>
        public static string ReturnPath(string? from, string fallbackFrom = FromAll) =>
            (NormalizeFrom(from) ?? NormalizeFrom(fallbackFrom) ?? FromAll) switch
            {
                FromPending => "/submission/pending",
                FromAiReview => "/admin/submission-review",
                _ => "/submission/index",
            };

        /// <summary>The TempData key the destination page shows its confirmation from.</summary>
        public static string MessageKey(string? from, string fallbackFrom = FromAll) =>
            (NormalizeFrom(from) ?? NormalizeFrom(fallbackFrom)) == FromAiReview ? "AiReviewMessage" : "SuccessMessage";

        /// <summary>
        /// A quick "Approve" that would change protected fields of an existing listing (main link,
        /// contact, PGP key, status ...) needs the admin to tick the confirmation box first.
        /// Returns the error to show, or null when the approve may go ahead.
        /// </summary>
        public static string? QuickApproveBlockReason(IReadOnlyCollection<string>? sensitiveChanges, bool confirmed)
        {
            if (sensitiveChanges == null || sensitiveChanges.Count == 0 || confirmed)
            {
                return null;
            }

            return "This approval changes protected fields of the listing (" + string.Join("; ", sensitiveChanges) +
                "). Tick \"I checked the protected-field changes\" next to Approve, then approve again.";
        }
    }
}
