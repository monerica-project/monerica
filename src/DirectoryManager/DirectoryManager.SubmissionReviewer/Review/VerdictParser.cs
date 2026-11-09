using System.Text.Json;
using System.Text.Json.Serialization;
using DirectoryManager.Data.Helpers;

namespace DirectoryManager.SubmissionReviewer.Review
{
    public static class VerdictParser
    {
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
            v.Issues ??= new List<string>();
            v.SuggestedSubcategories ??= new List<string>();
            v.Summary = (v.Summary ?? string.Empty).Trim();

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

        // Tolerates a stray code fence or text around the object.
        private static string ExtractJsonObject(string s)
        {
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            return start >= 0 && end > start ? s[start..(end + 1)] : s;
        }
    }
}
