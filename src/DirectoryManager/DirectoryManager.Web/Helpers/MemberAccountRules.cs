using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Rules for member accounts: usernames are 3–24 ASCII letters, digits, underscore or dash
    /// (so no look-alike Unicode), unique ignoring case, and may not be or imitate the site, its
    /// staff or an admin account; passwords are 10–128 characters with no other composition rules.
    /// </summary>
    public static class MemberAccountRules
    {
        public const int MinUsernameLength = 3;
        public const int MaxUsernameLength = 24;
        public const int MinPasswordLength = 10;
        public const int MaxPasswordLength = 128;

        public const string UsernameHelpText =
            "3–24 characters: letters, numbers, underscore (_) and dash (-). It is shown publicly on your reviews.";

        // Names that ARE one of these (after folding) are refused.
        private static readonly string[] ReservedExact =
        {
            "mod", "mods", "root", "system", "sysadmin", "owner", "owners", "anonymous", "anon", "null",
            "undefined", "security", "help", "team", "contact", "webmaster", "postmaster", "hostmaster",
            "abuse", "noreply", "info", "account", "accounts", "register", "login", "logout", "signin",
            "signup", "user", "users", "member", "members", "guest", "test", "listingowner", "reviewer",
            "operator", "service", "monero", "xmr",
        };

        // Names that CONTAIN one of these (after folding) are refused: they would read as the site,
        // its staff, a verified identity, or the "deleted member" placeholder.
        private static readonly string[] ReservedContains =
        {
            "admin", "administrator", "moderator", "support", "staff", "official", "sysop", "deleted",
            "verified", "pgp", "gpg", "fingerprint",
        };

        private static readonly Regex UsernamePattern = new ("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

        /// <summary>
        /// Returns null when <paramref name="username"/> is acceptable, otherwise a reason to show.
        /// <paramref name="extraReserved"/> adds names that must not be imitated: the configured
        /// site name and the existing admin accounts' usernames.
        /// </summary>
        public static string? ValidateUsername(string? username, IEnumerable<string?>? extraReserved = null)
        {
            var u = (username ?? string.Empty).Trim();
            if (u.Length < MinUsernameLength || u.Length > MaxUsernameLength)
            {
                return $"Your username must be {MinUsernameLength}–{MaxUsernameLength} characters.";
            }

            if (!UsernamePattern.IsMatch(u))
            {
                return "Your username may only contain letters, numbers, underscore (_) and dash (-).";
            }

            if (IsReserved(u, extraReserved))
            {
                return "That username is reserved. Please choose another one.";
            }

            return null;
        }

        public static bool IsReserved(string username, IEnumerable<string?>? extraReserved = null)
        {
            var folded = Fold(username);
            if (folded.Length == 0)
            {
                return true;
            }

            // A name made only of hex digits (8+) would read like a PGP key ID or fingerprint.
            var compact = new string((username ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray());
            if (compact.Length >= 8 && compact.All(char.IsAsciiHexDigit))
            {
                return true;
            }

            // The reserved words are folded the same way, so "official" and "0ff1c1al" compare equal.
            if (ReservedExact.Any(r => folded.Equals(Fold(r), StringComparison.Ordinal)))
            {
                return true;
            }

            if (ReservedContains.Any(r => folded.Contains(Fold(r), StringComparison.Ordinal)))
            {
                return true;
            }

            foreach (var extra in extraReserved ?? Enumerable.Empty<string?>())
            {
                foreach (var part in ReservedParts(extra))
                {
                    if (folded.Equals(part, StringComparison.Ordinal)
                        || (part.Length >= 4 && folded.Contains(part, StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns null when the password is acceptable, otherwise a reason to show.</summary>
        public static string? ValidatePassword(string? password, string? username = null)
        {
            var p = password ?? string.Empty;
            if (p.Length < MinPasswordLength)
            {
                return $"Your password must be at least {MinPasswordLength} characters.";
            }

            if (p.Length > MaxPasswordLength)
            {
                return $"Your password must be at most {MaxPasswordLength} characters.";
            }

            if (p.Distinct().Count() < 4)
            {
                return "Your password is too simple. Use a longer mix of characters, or keep the suggested one.";
            }

            if (!string.IsNullOrWhiteSpace(username) && p.Contains(username.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return "Your password must not contain your username.";
            }

            return null;
        }

        /// <summary>
        /// Lower-cases, keeps letters and digits only, and undoes common look-alike digits, so
        /// "Adm1n", "a_d_m_i_n" and "MODERAT0R" all fold to the reserved word they imitate.
        /// </summary>
        internal static string Fold(string? value)
        {
            var chars = (value ?? string.Empty).ToLowerInvariant()
                .Where(char.IsAsciiLetterOrDigit)
                .Select(c => c switch
                {
                    '0' => 'o',
                    '1' => 'i',
                    '3' => 'e',
                    '4' => 'a',
                    '5' => 's',
                    '7' => 't',
                    '8' => 'b',
                    _ => c,
                })
                .ToArray();
            return new string(chars).Replace("l", "i", StringComparison.Ordinal);
        }

        // An admin's username may be an email address: reserve both the whole thing and the part
        // before the "@" (folded). The site name is reserved as a whole and per word.
        private static IEnumerable<string> ReservedParts(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            var whole = Fold(value);
            if (whole.Length > 0)
            {
                yield return whole;
            }

            var at = value.IndexOf('@');
            if (at > 0)
            {
                var local = Fold(value[..at]);
                if (local.Length > 0)
                {
                    yield return local;
                }
            }

            foreach (var word in value.Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var w = Fold(word);
                if (w.Length >= 4)
                {
                    yield return w;
                }
            }
        }
    }
}
