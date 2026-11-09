using System.Text.Json.Serialization;

namespace DirectoryManager.SubmissionReviewer.Review
{
    public sealed class AiVerdict
    {
        public static readonly string[] Verdicts = { "approve_candidate", "flag", "reject" };
        public static readonly string[] Evidence = { "none", "mention_only", "checkout_or_instructions", "not_applicable" };

        [JsonPropertyName("verdict")]
        public string Verdict { get; set; } = "flag";

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("monero_evidence")]
        public string MoneroEvidence { get; set; } = "none";

        [JsonPropertyName("suggested_main_subcategory")]
        public string? SuggestedMainSubcategory { get; set; }

        [JsonPropertyName("suggested_subcategories")]
        public List<string> SuggestedSubcategories { get; set; } = new ();

        [JsonPropertyName("duplicate_of")]
        public string? DuplicateOf { get; set; }

        [JsonPropertyName("banned_category")]
        public bool BannedCategory { get; set; }

        [JsonPropertyName("issues")]
        public List<string> Issues { get; set; } = new ();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("proposal")]
        public AiProposal Proposal { get; set; } = new ();
    }

    /// <summary>Parses and sanity-checks the model's JSON. Anything malformed → null (the submission is Flagged).</summary>
}
