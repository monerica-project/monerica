using DirectoryManager.Data.Constants;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Which review tags count as a reported problem on the public incidents page: enabled,
    /// public (reviewer-selectable) tags at Warning level or above, such as "Funds held",
    /// "KYC Issue" or "Misleading fees". Money bands and the moderator-only "Suspicious Review"
    /// mark (about the review, not the service) never do. Based on the tag's level, not its name.
    /// The review repository's incident query applies the same conditions in SQL.
    /// </summary>
    public static class ProblemTagRule
    {
        public const ReviewTagLevel MinimumLevel = ReviewTagLevel.Warning;

        public static bool IsProblemTag(ReviewTag? tag) =>
            tag != null
            && tag.IsEnabled
            && tag.ReviewerSelectable
            && tag.Level >= MinimumLevel
            && !tag.IsMoneyBand
            && !string.Equals(tag.Slug, ReviewTagSlugs.Suspicious, StringComparison.OrdinalIgnoreCase);
    }
}
