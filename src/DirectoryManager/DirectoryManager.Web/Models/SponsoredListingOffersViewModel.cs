namespace DirectoryManager.Web.Models
{
    public class SponsoredListingOffersViewModel
    {
        public List<SponsoredListingOfferDisplayModel> MainSponsorshipOffers { get; set; } = new List<SponsoredListingOfferDisplayModel>();
        public List<SponsoredListingOfferDisplayModel> SubCategorySponsorshipOffers { get; set; } = new List<SponsoredListingOfferDisplayModel>();
    }
}