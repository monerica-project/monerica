using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.DetailChecker.Helpers
{
    /// <summary>One confident, human-reviewable proposed change to a listing field.</summary>
    public sealed record FieldChange(string Field, string? OldValue, string? NewValue, string Evidence);

    /// <summary>
    /// The outcome of reconfirming one listing: zero or more confident proposed changes
    /// (pre-filled into a revision the moderator can accept) plus zero or more uncertainties
    /// (things the automated check saw but will NOT decide — multiple countries, silent/unclear
    /// pages, contact info that doesn't match — left for a human, per project policy).
    /// </summary>
    public sealed class DetailDecision
    {
        public List<FieldChange> Changes { get; } = new();

        public List<string> Uncertainties { get; } = new();

        public bool HasAnything => this.Changes.Count > 0 || this.Uncertainties.Count > 0;
    }

    public static class DetailAnalyzer
    {
        // Propose an auto-fillable change only at/above this confidence.
        private const double ActThreshold = 0.7;

        // Surface a *genuine conflict* (e.g. site now multi-jurisdiction vs a stored single country)
        // for human review at/above this — but never a bare low-confidence guess.
        private const double FlagThreshold = 0.6;

        // Below this, a 1B model is essentially guessing; ignore its answer entirely rather than
        // flag noise. (The whole point: only confident, actionable findings reach the review queue.)
        private const double IgnoreBelow = 0.5;

        private static readonly HashSet<string> Iso2 = new(StringComparer.OrdinalIgnoreCase)
        {
            "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR", "AS", "AT", "AU", "AW", "AX", "AZ",
            "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI", "BJ", "BL", "BM", "BN", "BO", "BQ", "BR", "BS",
            "BT", "BV", "BW", "BY", "BZ", "CA", "CC", "CD", "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN",
            "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM", "DO", "DZ", "EC", "EE",
            "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK", "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF",
            "GG", "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM",
            "HN", "HR", "HT", "HU", "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR", "IS", "IT", "JE", "JM",
            "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN", "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC",
            "LI", "LK", "LR", "LS", "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MF", "MG", "MH", "MK",
            "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW", "MX", "MY", "MZ", "NA",
            "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP", "NR", "NU", "NZ", "OM", "PA", "PE", "PF", "PG",
            "PH", "PK", "PL", "PM", "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW",
            "SA", "SB", "SC", "SD", "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM", "SN", "SO", "SR", "SS",
            "ST", "SV", "SX", "SY", "SZ", "TC", "TD", "TF", "TG", "TH", "TJ", "TK", "TL", "TM", "TN", "TO",
            "TR", "TT", "TV", "TW", "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI",
            "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW",
        };

        public static DetailDecision Analyze(DirectoryEntry entry, string siteText, LlmResult? llm)
        {
            var d = new DetailDecision();

            AnalyzeCountry(entry, llm, d);
            AnalyzeKyc(entry, llm, d);
            AnalyzeEmail(entry, siteText, d);
            AnalyzeContacts(entry, siteText, d);

            return d;
        }

        public static KycPolicy? KycFromLlm(string? kyc) => kyc?.Trim().ToLowerInvariant() switch
        {
            "guaranteed_no" => KycPolicy.GuaranteedNoKyc,
            "rare" => KycPolicy.RareKyc,
            "shotgun" => KycPolicy.ShotgunKyc,
            "mandatory" => KycPolicy.MandatoryKyc,
            "varies" => KycPolicy.VariesByProvider,
            "not_stated" => KycPolicy.NotStated,
            _ => null, // "unclear" or unrecognised → no mapping
        };

        private static void AnalyzeCountry(DirectoryEntry entry, LlmResult? llm, DetailDecision d)
        {
            // Below the floor the small model is guessing — ignore it completely (no change, no flag).
            if (llm is null || llm.Confidence < IgnoreBelow)
            {
                return;
            }

            var code = llm.CountryCode?.Trim().ToUpperInvariant();
            var stored = entry.CountryCode?.Trim().ToUpperInvariant();

            // A real multi-jurisdiction read is only worth a human's time when it CONFLICTS with a
            // stored single country; otherwise it isn't actionable, so we stay quiet.
            if (llm.CountryMultiple)
            {
                if (llm.Confidence >= FlagThreshold && !string.IsNullOrEmpty(stored))
                {
                    d.Uncertainties.Add($"Country: site now reads as multiple/unclear jurisdictions, but stored is \"{stored}\". Review? {Quote(llm.Evidence)}");
                }

                return;
            }

            if (string.IsNullOrEmpty(code) || !Iso2.Contains(code))
            {
                return;
            }

            // Only a confident, differing single country becomes a proposed change. No low-confidence flags.
            if (llm.Confidence >= ActThreshold && !string.Equals(code, stored, StringComparison.Ordinal))
            {
                d.Changes.Add(new FieldChange("CountryCode", stored, code, Quote(llm.Evidence)));
            }
        }

        private static void AnalyzeKyc(DirectoryEntry entry, LlmResult? llm, DetailDecision d)
        {
            if (llm is null || llm.Confidence < IgnoreBelow)
            {
                return;
            }

            var mapped = KycFromLlm(llm.Kyc);

            // "unclear"/unrecognised means the MODEL couldn't decide — that's model weakness, not a
            // signal the SITE is ambiguous, so we stay silent. "not_stated" (silent page) likewise:
            // never downgrade a stored policy just because one page didn't mention it.
            if (mapped is null || mapped == KycPolicy.NotStated)
            {
                return;
            }

            // Only a confident, differing policy becomes a proposed change. No low-confidence flags.
            if (llm.Confidence >= ActThreshold && entry.KycPolicy != mapped)
            {
                d.Changes.Add(new FieldChange("KycPolicy", Describe(entry.KycPolicy), Describe(mapped), Quote(llm.Evidence)));
            }
        }

        private static void AnalyzeEmail(DirectoryEntry entry, string siteText, DetailDecision d)
        {
            var found = SnippetExtractor.ExtractEmails(siteText);
            if (found.Count == 0)
            {
                return;
            }

            var stored = entry.Email?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(stored))
            {
                // Only auto-propose when there's a single obvious address; otherwise flag the options.
                if (found.Count == 1)
                {
                    d.Changes.Add(new FieldChange("Email", null, found[0], "found on site"));
                }
                else
                {
                    d.Uncertainties.Add($"Email: none stored; site lists {found.Count}: {string.Join(", ", found.Take(5))}. Pick one?");
                }
            }
            else if (!found.Contains(stored))
            {
                // Never auto-overwrite a contact email — just flag the mismatch.
                d.Uncertainties.Add($"Email: stored \"{stored}\" not found on site; site lists: {string.Join(", ", found.Take(5))}. Changed?");
            }
        }

        private static void AnalyzeContacts(DirectoryEntry entry, string siteText, DetailDecision d)
        {
            var found = SnippetExtractor.ExtractContacts(siteText);
            if (found.Count == 0)
            {
                return;
            }

            // Contact/messenger/social mapping is ambiguous (which handle is "Messenger" vs
            // "Social"?), so we never auto-set these — we just surface what's on the site when
            // the listing has neither recorded, for a human to slot in.
            var hasContact = !string.IsNullOrWhiteSpace(entry.Messenger) || !string.IsNullOrWhiteSpace(entry.Social);
            if (!hasContact)
            {
                d.Uncertainties.Add($"Contacts: site lists {string.Join(", ", found.Take(5))} but none recorded. Add?");
            }
        }

        private static string Describe(KycPolicy? p) => p switch
        {
            null or KycPolicy.NotStated => "Not Stated",
            KycPolicy.GuaranteedNoKyc => "Guaranteed No KYC",
            KycPolicy.RareKyc => "Rare KYC",
            KycPolicy.ShotgunKyc => "Shotgun KYC",
            KycPolicy.MandatoryKyc => "Mandatory KYC",
            KycPolicy.VariesByProvider => "Varies By Provider",
            _ => p.ToString() ?? "Unknown",
        };

        private static string Quote(string? evidence)
            => string.IsNullOrWhiteSpace(evidence) ? string.Empty : $"“{evidence.Trim().Replace('\n', ' ')}”";
    }
}
