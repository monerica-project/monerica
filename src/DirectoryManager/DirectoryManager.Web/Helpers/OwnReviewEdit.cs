using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Rules for a review's AUTHOR adjusting their own rating and tags while replying in the
    /// review's thread (the written review itself never changes). Pure functions; the reply flow
    /// and the reply moderation page call them.
    /// </summary>
    public static class OwnReviewEdit
    {
        /// <summary>A rating the author may set: 1–5, and only when it differs from the current one.</summary>
        public static byte? RatingChange(byte? current, byte? requested) =>
            requested is >= 1 and <= 5 && requested != current ? requested : null;

        /// <summary>
        /// The author's requested set of PUBLIC (reviewer-selectable, enabled) tags: unknown,
        /// disabled and admin-only ids are dropped, duplicates removed, and at most ONE money band
        /// kept (the dropdown's choice wins). Sorted for a stable comparison/CSV.
        /// </summary>
        public static List<int> RequestedTags(
            IEnumerable<ReviewTag> enabledTags,
            IEnumerable<int>? checkedTagIds,
            int? moneyBandTagId)
        {
            var selectable = enabledTags.Where(t => t.ReviewerSelectable).ToDictionary(t => t.ReviewTagId);

            var picked = (checkedTagIds ?? Enumerable.Empty<int>())
                .Where(id => selectable.TryGetValue(id, out var t) && !t.IsMoneyBand)
                .ToList();

            if (moneyBandTagId is int band && selectable.TryGetValue(band, out var bandTag) && bandTag.IsMoneyBand)
            {
                picked.Add(band);
            }

            return picked.Distinct().OrderBy(id => id).ToList();
        }

        /// <summary>The public tags the review carries now (admin-only tags excluded), sorted.</summary>
        public static List<int> CurrentPublicTags(IEnumerable<int> currentTagIds, IEnumerable<ReviewTag> allTags)
        {
            var selectable = allTags.Where(t => t.ReviewerSelectable).Select(t => t.ReviewTagId).ToHashSet();
            return currentTagIds.Where(selectable.Contains).Distinct().OrderBy(id => id).ToList();
        }

        /// <summary>
        /// The review's full tag set after an approved change: every admin-only tag it already has
        /// (e.g. "Valid Order", "Suspicious Review") is kept; the public tags become the author's request.
        /// </summary>
        public static List<int> ApplyRequested(IEnumerable<int> currentTagIds, IEnumerable<ReviewTag> allTags, IEnumerable<int> requested)
        {
            var selectable = allTags.Where(t => t.ReviewerSelectable).Select(t => t.ReviewTagId).ToHashSet();
            return currentTagIds.Where(id => !selectable.Contains(id))
                .Concat(requested.Where(selectable.Contains))
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }

        public static string ToCsv(IEnumerable<int> ids) => string.Join(",", ids);

        public static List<int> FromCsv(string? csv) =>
            (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? v : 0)
                .Where(v => v > 0)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
    }
}
