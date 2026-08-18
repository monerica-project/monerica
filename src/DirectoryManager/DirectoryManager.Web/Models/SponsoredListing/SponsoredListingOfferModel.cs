using DirectoryManager.Data.Enums;
using Newtonsoft.Json;

namespace DirectoryManager.Web.Models.SponsoredListing
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

        /// <summary>
        /// Cross-tier existing-sponsor perk percent applied to this price (e.g. 15). 0 = no discount.
        /// </summary>
        [JsonProperty("discountPercent")]
        public decimal DiscountPercent { get; set; }

        /// <summary>
        /// The price after the cross-tier existing-sponsor perk. Equals <see cref="USDPrice"/> when there is no discount.
        /// </summary>
        [JsonProperty("discountedUSDPrice")]
        public decimal DiscountedUSDPrice { get; set; }

        [JsonIgnore]
        public bool HasExistingSponsorPerk => this.DiscountPercent > 0m;
    }
}