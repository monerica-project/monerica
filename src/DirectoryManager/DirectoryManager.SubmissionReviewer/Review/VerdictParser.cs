using System.Text.Json;
using System.Text.Json.Serialization;
using DirectoryManager.Data.Helpers;

namespace DirectoryManager.SubmissionReviewer.Review
{
    public static class VerdictParser
    {
        public const int MaxModelIssues = 3;
        public const int MaxIssueChars = 160;
        public const int MaxSummaryChars = 260;

        public static AiVerdict? TryParse(string? json, out string? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty response";
                return null;
            }

            AiVerdict? v;
            try
            {
                v = JsonSerializer.Deserialize<AiVerdict>(ExtractJsonObject(json));
            }
            catch (JsonException ex)
            {
                error = "Malformed JSON: " + ex.Message;
                return null;
            }

            if (v == null)
            {
                error = "Empty JSON";
                return null;
            }

            v.Verdict = (v.Verdict ?? string.Empty).Trim().ToLowerInvariant();
            if (!AiVerdict.Verdicts.Contains(v.Verdict))
            {
                error = $"Unknown verdict \"{v.Verdict}\"";
                return null;
            }

            // The owner reads these on a busy page: keep the model's own points few and short.
            // (Points added by the checks below are kept in full.)
            v.Issues = (v.Issues ?? new List<string>())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Take(MaxModelIssues)
                .Select(i => Shorten(i.Trim(), MaxIssueChars))
                .ToList();

            v.MoneroEvidence = (v.MoneroEvidence ?? "none").Trim().ToLowerInvariant();
            if (!AiVerdict.Evidence.Contains(v.MoneroEvidence))
            {
                v.Issues.Add($"Reviewer returned unknown monero_evidence \"{v.MoneroEvidence}\"");
                v.MoneroEvidence = "none";
            }

            if (double.IsNaN(v.Confidence) || v.Confidence < 0 || v.Confidence > 1)
            {
                v.Issues.Add($"Confidence out of range ({v.Confidence}); set to 0");
                v.Confidence = 0;
            }

            v.Proposal ??= new AiProposal();
            v.SuggestedSubcategories ??= new List<string>();
            v.Summary = Shorten((v.Summary ?? string.Empty).Trim(), MaxSummaryChars);

            var desc = v.Proposal.Description?.Trim();
            if (desc != null && desc.Length > ListingText.DescriptionMaxLength)
            {
                v.Issues.Add($"Suggested description was {desc.Length} characters (limit {ListingText.DescriptionMaxLength}); not used");
                v.Proposal.Description = null;
            }

            var note = v.Proposal.Note?.Trim();
            if (note != null && note.Length > ListingText.NoteMaxLength)
            {
                v.Issues.Add($"Suggested note was {note.Length} characters (limit {ListingText.NoteMaxLength}); not used");
                v.Proposal.Note = null;
            }

            if (v.Proposal.CountryCode is { } cc && !System.Text.RegularExpressions.Regex.IsMatch(cc.Trim(), "^[A-Za-z]{2}$"))
            {
                v.Proposal.CountryCode = null;
            }

            return v;
        }

        // Cuts at a word boundary so a long answer still reads cleanly.
        private static string Shorten(string s, int max)
        {
            if (s.Length <= max)
            {
                return s;
            }

            var cut = s.LastIndexOf(' ', max - 1);
            return s[..(cut > max / 2 ? cut : max - 1)].TrimEnd(' ', ',', ';', ':') + "…";
        }

        // Tolerates a stray code fence or text around the object.
        private static string ExtractJsonObject(string s)
        {
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            return start >= 0 && end > start ? s[start..(end + 1)] : s;
        }
    }
}
