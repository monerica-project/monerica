using System;

namespace DirectoryManager.Utilities.Helpers
{
    public static class ReferralCodeHelper
    {
        /// <summary>
        /// Try to normalize a referral code. Returns true if valid (or empty/null), and outputs normalized lowercase code.
        /// If <paramref name="raw"/> is null/empty/whitespace, this returns true with <paramref name="normalized"/> = null.
        /// </summary>
        public static bool TryNormalize(string? raw, out string? normalized, out string? error)
        {
            normalized = null;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                // optional field → treat as “no code supplied”
                return true;
            }

            var s = raw.Trim().ToLowerInvariant();

            if (s.Length < 3 || s.Length > 12)
            {
                error = "Referral code must be 3–12 characters.";
                return false;
            }

            foreach (var ch in s)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    error = "Referral code may contain letters and numbers only.";
                    return false;
                }
            }

            normalized = s;
            return true;
        }

        /// <summary>
        /// Shortcut: returns normalized lowercase code, or null if invalid/empty.
        /// </summary>
        public static string? NormalizeOrNull(string? raw)
        {
            return TryNormalize(raw, out var norm, out _) ? norm : null;
        }
    }
}