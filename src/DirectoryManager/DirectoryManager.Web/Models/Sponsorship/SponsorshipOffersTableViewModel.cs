using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Models.SponsoredListing;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipOffersTableViewModel
    {
        public SponsorshipType SponsorshipType { get; set; }

        public List<SponsoredListingOfferDisplayModel> Offers { get; set; } = new List<SponsoredListingOfferDisplayModel>();

        public decimal ConversionRate { get; set; }

        public string SelectedCurrency { get; set; } = "XMR";
    }
}
