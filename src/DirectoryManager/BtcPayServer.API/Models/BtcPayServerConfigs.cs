namespace BtcPayServer.API.Models
{
    public class BtcPayServerConfigs
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public string DefaultCurrency { get; set; } = "USD";
    }
}