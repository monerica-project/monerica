using Newtonsoft.Json;

namespace DirectoryManager.Web.Models
{
    public class SponsoredListingOfferModel
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("USDPrice")]
        public decimal USDPrice { get; set; }

        [JsonProperty("description")]
        required public string Description { get; set; }

        [JsonProperty("days")]
        public int Days { get; set; }
    }
}