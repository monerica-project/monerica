using System.Text.Json.Serialization;

namespace DirectoryManager.SubmissionReviewer.Review
{
    /// <summary>The reviewer's suggested version of the listing. Null = keep what was submitted.</summary>
    public sealed class AiProposal
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("kyc_policy")]
        public string? KycPolicy { get; set; }

        [JsonPropertyName("liquidity")]
        public string? Liquidity { get; set; }

        [JsonPropertyName("rate_type")]
        public string? RateType { get; set; }

        [JsonPropertyName("site_javascript")]
        public string? SiteJavaScript { get; set; }

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("changes")]
        public List<ProposedChange> Changes { get; set; } = new ();
    }
}
