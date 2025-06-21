namespace DirectoryManager.Web.Models
{
    public class ActiveSponsoredListingViewModel
    {
        public List<ActiveSponsoredListingModel> MainSponsorItems { get; set; } = new List<ActiveSponsoredListingModel>();
        public List<ActiveSponsoredListingModel> SubCategorySponsorItems { get; set; } = new List<ActiveSponsoredListingModel>();
        public List<ActiveSponsoredListingModel> CategorySponsorItems { get; set; } = new List<ActiveSponsoredListingModel>();
    }
}