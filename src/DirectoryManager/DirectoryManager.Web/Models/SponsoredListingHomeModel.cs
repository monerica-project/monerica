namespace DirectoryManager.Web.Models
{
    public class SponsoredListingHomeModel
    {
        public bool CanCreateSponsoredListing { get; set; }
        public DateTime NextListingExpiration { get; set; }
        public int CurrentListingCount { get; set; }
        public string? Message { get; set; }
    }
}