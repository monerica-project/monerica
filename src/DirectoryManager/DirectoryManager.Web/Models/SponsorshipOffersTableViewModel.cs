using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class SponsorshipOffersTableViewModel
    {
        public SponsorshipType SponsorshipType { get; set; }

        public List<SponsoredListingOfferDisplayModel> Offers { get; set; } = new List<SponsoredListingOfferDisplayModel>();

        public decimal ConversionRate { get; set; }

        public string SelectedCurrency { get; set; } = "XMR";
    }
}
