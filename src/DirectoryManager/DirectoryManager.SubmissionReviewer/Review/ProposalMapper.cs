using System.Text.Json;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.SubmissionReviewer.Review
{
    /// <summary>Writes a verdict onto the submission's review/Proposed* fields. Never touches the submitted fields.</summary>
    public static class ProposalMapper
    {
        public static SubmissionReviewState StateFor(AiVerdict v) =>
            v.BannedCategory ? SubmissionReviewState.Flagged
            : v.Verdict switch
            {
                "approve_candidate" => SubmissionReviewState.AiApproveCandidate,
                "reject" => SubmissionReviewState.AiRejectCandidate,
                _ => SubmissionReviewState.Flagged,
            };

        /// <param name="subcategoryIdsByName">"Category > Subcategory" (case-insensitive) → id.</param>
        public static void Apply(Submission s, AiVerdict v, IReadOnlyDictionary<string, int> subcategoryIdsByName)
        {
            var p = v.Proposal;
            s.ProposedName = Clean(p.Name, 255) ?? s.Name;
            s.ProposedDescription = ListingText.RemoveExclamations(Clean(p.Description, 175) ?? s.Description);
            // The note is always the reviewer's final public note: null means "no note", so a
            // promotional or rule-breaking submitted note is never carried over by default.
            s.ProposedNote = ListingText.RemoveExclamations(Clean(p.Note, 250));
            s.ProposedCountryCode = Clean(p.CountryCode, 2)?.ToUpperInvariant() ?? s.CountryCode;
            s.ProposedLocation = Clean(p.Location, 255) ?? s.Location;
            s.ProposedTags = Clean(p.Tags, 255) ?? s.Tags;
            s.ProposedKycPolicy = ParseEnum<KycPolicy>(p.KycPolicy) ?? s.KycPolicy;
            s.ProposedLiquidity = ParseEnum<Liquidity>(p.Liquidity) ?? (s.Liquidity == Liquidity.Unknown ? null : s.Liquidity);
            s.ProposedRateType = ParseEnum<RateType>(p.RateType) ?? (s.RateType == RateType.Unknown ? null : s.RateType);
            s.ProposedSiteJavaScript = ParseEnum<SiteJavaScript>(p.SiteJavaScript) ?? (s.SiteJavaScript == SiteJavaScript.Unknown ? null : s.SiteJavaScript);

            if (!string.IsNullOrWhiteSpace(v.SuggestedMainSubcategory) &&
                subcategoryIdsByName.TryGetValue(v.SuggestedMainSubcategory.Trim(), out var subId))
            {
                s.ProposedSubCategoryId = subId;
            }
            else
            {
                s.ProposedSubCategoryId = s.SubCategoryId;
                if (!string.IsNullOrWhiteSpace(v.SuggestedMainSubcategory))
                {
                    v.Issues.Add($"Suggested subcategory \"{v.SuggestedMainSubcategory}\" is not in the list; kept the submitted one");
                }
            }

            var others = v.SuggestedSubcategories
                .Select(x => x.Trim())
                .Where(x => x.Length > 0 && subcategoryIdsByName.TryGetValue(x, out var id) && id != s.ProposedSubCategoryId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();
            s.ProposedOtherSubcategories = others.Count == 0 ? null : Clean(string.Join("; ", others), 500);

            s.ProposedChangesJson = p.Changes.Count == 0 ? null : JsonSerializer.Serialize(p.Changes);
            s.ReviewVerdict = v.Verdict;
            s.ReviewConfidence = (decimal)Math.Round(v.Confidence, 3);
            s.ReviewMoneroEvidence = v.MoneroEvidence;
            s.ReviewSummary = Clean(v.Summary, 1000);
            s.ReviewIssuesJson = v.Issues.Count == 0 ? null : JsonSerializer.Serialize(v.Issues);
            s.ReviewDuplicateOf = Clean(v.DuplicateOf, 500);
            s.ReviewBannedCategory = v.BannedCategory;
            s.ReviewState = StateFor(v);
        }

        private static T? ParseEnum<T>(string? value)
            where T : struct, Enum =>
            !string.IsNullOrWhiteSpace(value) && Enum.TryParse<T>(value.Trim(), true, out var e) && Enum.IsDefined(e) ? e : null;

        private static string? Clean(string? s, int max)
        {
            var t = s?.Trim();
            return string.IsNullOrEmpty(t) ? null : t.Length <= max ? t : t[..max];
        }
    }
}
