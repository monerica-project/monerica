namespace DirectoryManager.Web.Models.SponsoredListing
{
    public class SponsoredListingOffersViewModel
    {
        public List<SponsoredListingOfferDisplayModel> MainSponsorshipOffers { get; set; } = new List<SponsoredListingOfferDisplayModel>();
        public List<SponsoredListingOfferDisplayModel> SubCategorySponsorshipOffers { get; set; } = new List<SponsoredListingOfferDisplayModel>();
        public List<SponsoredListingOfferDisplayModel> CategorySponsorshipOffers { get; set; } = new List<SponsoredListingOfferDisplayModel>();
        public int ConversionRate { get; internal set; }
        public string SelectedCurrency { get; internal set; }
        public DateTime? LastUpdatedDate { get; set; }
    }
}