using System.Text.Json.Serialization;

namespace DirectoryManager.SubmissionReviewer.Review
{
    /// <summary>One field the reviewer changed (or checked and left), with why.</summary>
    public sealed class ProposedChange
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
