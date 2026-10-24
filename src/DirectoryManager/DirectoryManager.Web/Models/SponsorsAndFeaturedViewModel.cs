namespace DirectoryManager.Web.Models
{
    /// <summary>
    /// /sponsors-and-featured: every paid sponsor and every unpaid featured pick on one page.
    /// </summary>
    public class SponsorsAndFeaturedViewModel
    {
        public string SiteName { get; set; } = string.Empty;

        public List<SpotListing> MainSponsors { get; set; } = new ();

        public List<SpotCategoryGroup> Categories { get; set; } = new ();

        public int SponsoredSpotCount { get; set; }

        public int FeaturedSpotCount { get; set; }
    }
}
