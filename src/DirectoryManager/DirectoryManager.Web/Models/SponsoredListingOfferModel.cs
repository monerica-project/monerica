using DirectoryManager.Data.Enums;
using Newtonsoft.Json;

namespace DirectoryManager.Web.Models
{
    public class SponsoredListingOfferModel
    {
        [JsonProperty("sponsoredListingOfferId")]
        public int SponsoredListingOfferId { get; set; }

        [JsonProperty(nameof(USDPrice))]
        public decimal USDPrice { get; set; }

        [JsonProperty("description")]
        required public string Description { get; set; }

        [JsonProperty("days")]
        public int Days { get; set; }

        [JsonProperty("sponsorshipType")]
        public SponsorshipType SponsorshipType { get; set; }
    }
}