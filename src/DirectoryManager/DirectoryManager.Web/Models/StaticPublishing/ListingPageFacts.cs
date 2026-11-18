namespace DirectoryManager.Web.Models.StaticPublishing
{
    /// <summary>Everything needed to list the public pages one listing appears on.</summary>
    public class ListingPageFacts
    {
        public string EntryKey { get; set; } = string.Empty;

        /// <summary>Approved crowd reviews (official reviews are pinned on page 1 and not paged).</summary>
        public int CrowdReviewCount { get; set; }

        public List<ListingPlacementFacts> Placements { get; set; } = new List<ListingPlacementFacts>();

        public List<PagedPageFacts> Tags { get; set; } = new List<PagedPageFacts>();

        /// <summary>Total tags with listings (pages of the /tagged index).</summary>
        public int TagCount { get; set; }

        public PagedPageFacts? Country { get; set; }

        /// <summary>Countries with listings (pages of the /countries index).</summary>
        public int CountryCount { get; set; }
    }
}
