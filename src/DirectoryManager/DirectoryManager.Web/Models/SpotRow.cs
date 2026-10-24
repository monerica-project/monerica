namespace DirectoryManager.Web.Models
{
    /// <summary>One sponsor spot (a category page or a subcategory) and what fills it.</summary>
    public class SpotRow
    {
        /// <summary>"Category page" for the category spot, or the subcategory name.</summary>
        public string SpotName { get; set; } = string.Empty;

        public string SpotPath { get; set; } = string.Empty;

        public bool IsCategorySpot { get; set; }

        /// <summary>True = paid sponsor(s); false = our unpaid featured pick.</summary>
        public bool IsSponsored { get; set; }

        public List<SpotListing> Listings { get; set; } = new ();
    }
}
