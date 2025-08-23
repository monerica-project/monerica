using Newtonsoft.Json;

namespace DirectoryManager.Web.Models
{
    public class VerifyResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("score")]
        public double? Score { get; set; } // reCAPTCHA v3

        [JsonProperty("action")]
        public string[]? Action { get; set; }

        [JsonProperty("error_codes")]
        public string[]? ErrorCodes { get; set; }
    }
}