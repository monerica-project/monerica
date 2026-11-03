namespace DirectoryManager.Data.Constants
{
    /// <summary>Stable slugs of review tags that code depends on (ReviewTags.Slug).</summary>
    public static class ReviewTagSlugs
    {
        /// <summary>The reviewer's order link was checked by hand: a real order happened.</summary>
        public const string ValidOrder = "valid-order";

        /// <summary>Flags the review itself as suspicious; not a problem with the service.</summary>
        public const string Suspicious = "suspicious";
    }
}
