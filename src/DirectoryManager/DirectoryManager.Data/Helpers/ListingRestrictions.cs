using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// A listing's usage restrictions: the countries it does not serve, whether a VPN or Tor may be
    /// used, and a short free-text "other" line. One place for the input rules (forms), the stored
    /// form (a sorted comma-separated list of ISO 3166-1 alpha-2 codes, e.g. "CU,IR,KP") and the
    /// profile wording, so the admin form, the public forms and the listing page always agree.
    /// </summary>
    public static class ListingRestrictions
    {
        /// <summary>Database limit for the stored country-code list (every country fits in ~750).</summary>
        public const int RestrictedCountriesMaxLength = 1000;

        /// <summary>Limit for the "other restrictions" line (database and forms).</summary>
        public const int OtherRestrictionsMaxLength = 200;

        /// <summary>The "other" text goes straight into the summary line when it is this short or shorter.</summary>
        public const int InlineOtherMaxLength = 40;

        /// <summary>Up to this many countries are named in the summary line; more are counted.</summary>
        public const int NamedCountriesInSummary = 2;

        public const string Separator = " · ";

        private static readonly char[] InputSeparators = { ',', ';', '\n', '\r', '|' };

        /// <summary>
        /// Turns form input ("US, iran, North Korea" or "us,ir") into the stored form ("IR,KP,US").
        /// Accepts two-letter codes, country names and the usual aliases ("USA", "UK"). Duplicates
        /// are dropped; blank input gives a null list.
        /// </summary>
        /// <returns>False when any entry is not a known country (listed in <paramref name="unknown"/>).</returns>
        public static bool TryNormalizeCountries(string? input, out string? normalized, out IReadOnlyList<string> unknown)
        {
            var codes = new SortedSet<string>(StringComparer.Ordinal);
            var bad = new List<string>();

            foreach (var raw in (input ?? string.Empty).Split(InputSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim().TrimEnd('.').Trim();
                if (token.Length == 0)
                {
                    continue;
                }

                var code = ResolveCountry(token);
                if (code == null)
                {
                    if (!bad.Contains(token, StringComparer.OrdinalIgnoreCase))
                    {
                        bad.Add(token);
                    }

                    continue;
                }

                codes.Add(code);
            }

            normalized = codes.Count == 0 ? null : string.Join(",", codes);
            unknown = bad;
            return bad.Count == 0;
        }

        /// <summary>The stored list normalized again (or null), ignoring anything unknown. For values already validated.</summary>
        /// <returns>The normalized list.</returns>
        public static string? NormalizeCountries(string? input)
        {
            TryNormalizeCountries(input, out var normalized, out _);
            return normalized;
        }

        /// <summary>The form's error text for unknown entries, or null when every entry is a country.</summary>
        /// <returns>The message.</returns>
        public static string? CountriesError(string? input)
        {
            if (TryNormalizeCountries(input, out var normalized, out var unknown))
            {
                return normalized != null && normalized.Length > RestrictedCountriesMaxLength
                    ? "Too many countries listed."
                    : null;
            }

            var shown = string.Join(", ", unknown.Take(5).Select(u => "\"" + (u.Length > 40 ? u.Substring(0, 40) + "…" : u) + "\""));
            return $"Not a recognized country: {shown}. Use two-letter country codes (e.g. US, GB) or full country names, separated by commas.";
        }

        /// <summary>The ISO codes in a stored list (known codes only, upper case, no duplicates).</summary>
        /// <returns>The codes, in stored order.</returns>
        public static IReadOnlyList<string> ParseCodes(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
            {
                return Array.Empty<string>();
            }

            var countries = CountryHelper.GetCountries();
            return stored
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .Where(c => countries.ContainsKey(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Country names for a stored list, sorted A–Z ("Cuba", "Iran", "North Korea").</summary>
        /// <returns>The names.</returns>
        public static IReadOnlyList<string> CountryNames(string? stored) =>
            ParseCodes(stored)
                .Select(CountryHelper.GetCountryName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>What the form's text box shows for a stored list: the names, comma-separated.</summary>
        /// <returns>The text, or null for an empty list.</returns>
        public static string? ToInputText(string? stored)
        {
            var names = CountryNames(stored);
            return names.Count == 0 ? null : string.Join(", ", names);
        }

        /// <summary>The stored list as compact codes for an admin text box ("CU, IR, KP"), or null.</summary>
        /// <returns>The text.</returns>
        public static string? ToCodeText(string? stored)
        {
            var codes = ParseCodes(stored);
            return codes.Count == 0 ? null : string.Join(", ", codes);
        }

        /// <summary>Trimmed single-line "other restrictions" text, or null when blank.</summary>
        /// <returns>The cleaned text.</returns>
        public static string? CleanOther(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var s = string.Join(" ", value.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0));
            return s.Length == 0 ? null : s;
        }

        /// <summary>True when any of the links is a Tor (.onion) address.</summary>
        /// <returns>Whether the listing has an onion link.</returns>
        public static bool HasOnionLink(params string?[] links) =>
            links.Any(l => !string.IsNullOrWhiteSpace(l) && l.Contains(".onion", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The form error when Tor is marked "Not Allowed" on a listing that has a .onion link (it
        /// plainly works over Tor), else null.
        /// </summary>
        /// <returns>The message, or null.</returns>
        public static string? TorConflictError(TorPolicy tor, params string?[] links) =>
            tor == TorPolicy.NotAllowed && HasOnionLink(links)
                ? "Tor can't be \"Not Allowed\" for a listing with a .onion link. Choose another Tor option or remove the .onion link."
                : null;

        /// <summary>Lower-case phrase for a VPN/Tor answer, e.g. "not allowed"; null for Not Stated.</summary>
        /// <returns>The phrase.</returns>
        public static string? Phrase(VpnPolicy value) => value switch
        {
            VpnPolicy.Allowed => "allowed",
            VpnPolicy.NotAllowed => "not allowed",
            VpnPolicy.MayTriggerVerification => "may trigger verification",
            _ => null,
        };

        /// <summary>Lower-case phrase for a VPN/Tor answer, e.g. "not allowed"; null for Not Stated.</summary>
        /// <returns>The phrase.</returns>
        public static string? Phrase(TorPolicy value) => value switch
        {
            TorPolicy.Allowed => "allowed",
            TorPolicy.NotAllowed => "not allowed",
            TorPolicy.MayTriggerVerification => "may trigger verification",
            _ => null,
        };

        /// <summary>
        /// Everything the listing page needs for the restrictions blurb. A Tor "Not Allowed" on a
        /// listing with a .onion link is dropped rather than shown next to the onion link.
        /// </summary>
        /// <returns>The summary (check <see cref="RestrictionsSummary.IsEmpty"/>).</returns>
        public static RestrictionsSummary Summarize(string? restrictedCountries, VpnPolicy vpn, TorPolicy tor, string? other, bool hasOnionLink)
        {
            var names = CountryNames(restrictedCountries);
            var vpnPhrase = Phrase(vpn);
            var torPhrase = tor == TorPolicy.NotAllowed && hasOnionLink ? null : Phrase(tor);
            var otherText = CleanOther(other);

            var segments = new List<string>();
            if (names.Count > NamedCountriesInSummary)
            {
                segments.Add($"{names.Count} countries not served");
            }
            else if (names.Count > 0)
            {
                segments.Add("Not served in " + string.Join(" and ", names));
            }

            if (vpnPhrase != null)
            {
                segments.Add("VPN " + vpnPhrase);
            }

            if (torPhrase != null)
            {
                segments.Add("Tor " + torPhrase);
            }

            var otherInline = otherText != null && otherText.Length <= InlineOtherMaxLength;
            if (otherText != null)
            {
                segments.Add(otherInline ? otherText : "other restrictions");
            }

            var line = string.Join(Separator, segments);
            if (line.Length > 0)
            {
                line = char.ToUpperInvariant(line[0]) + line.Substring(1);
            }

            return new RestrictionsSummary
            {
                SummaryLine = line,
                CountryNames = names,
                Vpn = vpnPhrase == null ? null : Capitalize(vpnPhrase),
                Tor = torPhrase == null ? null : Capitalize(torPhrase),
                Other = otherText,
                NeedsDetails = names.Count > NamedCountriesInSummary || (otherText != null && !otherInline),
            };
        }

        /// <summary>
        /// Every recorded restriction in one plain line for admin pages and the AI reviewer, e.g.
        /// "Not served in: Cuba, Iran; VPN: Not allowed; Other: Minimum age 18". Null when nothing is recorded.
        /// </summary>
        /// <returns>The text, or null.</returns>
        public static string? FullText(string? restrictedCountries, VpnPolicy vpn, TorPolicy tor, string? other)
        {
            var r = Summarize(restrictedCountries, vpn, tor, other, hasOnionLink: false);
            var parts = new List<string>();
            if (r.CountryNames.Count > 0)
            {
                parts.Add("Not served in: " + string.Join(", ", r.CountryNames));
            }

            if (r.Vpn != null)
            {
                parts.Add("VPN: " + r.Vpn);
            }

            if (r.Tor != null)
            {
                parts.Add("Tor: " + r.Tor);
            }

            if (r.Other != null)
            {
                parts.Add("Other: " + r.Other);
            }

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static string? ResolveCountry(string token)
        {
            var countries = CountryHelper.GetCountries();
            if (token.Length == 2 && countries.ContainsKey(token.ToUpperInvariant()))
            {
                return token.ToUpperInvariant();
            }

            if (CountryHelper.TryGetExactCountryCode(token, out var code) && countries.ContainsKey(code))
            {
                return code;
            }

            if (token.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
                && CountryHelper.TryGetExactCountryCode(token.Substring(4), out var withoutThe)
                && countries.ContainsKey(withoutThe))
            {
                return withoutThe;
            }

            return null;
        }
    }
}
