namespace BtcPayServer.API.Models
{
    public class BtcPayServerConfigs
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;

        // Separate "Monerica - ReviewRequests" store (optional donations tied to a
        // verification request). Same server + account-level ApiKey as StoreId above.
        public string ReviewDonationsStoreId { get; set; } = string.Empty;

        // Separate store for video sponsorships (its own wallet and webhook secret).
        // The store ID and webhook secret must be set for video-sponsor checkout to be available;
        // VideoSponsorApiKey is optional and falls back to the account-level ApiKey above.
        public string VideoSponsorStoreId { get; set; } = string.Empty;
        public string VideoSponsorApiKey { get; set; } = string.Empty;
        public string VideoSponsorWebhookSecret { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public string DefaultCurrency { get; set; } = "USD";
    }
}