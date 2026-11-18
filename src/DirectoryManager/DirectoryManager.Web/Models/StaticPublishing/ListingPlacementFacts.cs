namespace DirectoryManager.Web.Models.StaticPublishing
{
    /// <summary>A subcategory a listing appears in (main or additional), with the counts that decide
    /// how many paginated pages its subcategory and category have.</summary>
    public class ListingPlacementFacts
    {
        public string CategoryKey { get; set; } = string.Empty;

        public int CategoryEntryCount { get; set; }

        public string SubcategoryKey { get; set; } = string.Empty;

        public int SubcategoryEntryCount { get; set; }
    }
}
