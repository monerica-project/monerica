using Newtonsoft.Json;

namespace DirectoryManager.Web.Models
{
    public class SponsoredListingOffer
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("USDPrice")]
        public decimal USDPrice { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}