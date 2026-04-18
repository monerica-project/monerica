using Newtonsoft.Json;

namespace DirectoryManager.Console.Models
{
    public class UserAgentModel
    {
        [JsonProperty("pattern")]
        public string? Pattern { get; set; }

        [JsonProperty("addition_date")]
        public string? AdditionDate { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("instances")]

        public List<string>? Instances { get; set; }
    }
}