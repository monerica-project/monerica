namespace DirectoryManager.SubmissionReviewer.Review
{
    public sealed class ReviewerOptions
    {
        public string Model { get; set; } = "claude-sonnet-5-5";

        // The verdict plus the suggested listing fields fit in well under 1,000 tokens.
        public int MaxTokens { get; set; } = 4000;

        // Bill protection: AI reviews per UTC day, and per run (the job runs every 10 minutes).
        public int DailyCap { get; set; } = 50;

        public int PerRunCap { get; set; } = 10;

        public int MaxApiAttempts { get; set; } = 4;

        // A draft left in Preview this long is sent for review as if the submitter had pressed Submit.
        public int PreviewPromoteAfterHours { get; set; } = 24;

        // Only drafts started after this are auto-submitted; older previews were already handled by hand.
        public DateTime PreviewPromoteSinceUtc { get; set; } = new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc);

        public int DossierMaxChars { get; set; } = 6000;

        public string UserAgent { get; set; } = "MonericaSubmissionReviewer/1.0 (+https://monerica.com; reviews directory submissions)";

        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.anthropic.com/";

        public string TorHost { get; set; } = "127.0.0.1";

        public int TorPort { get; set; } = 9050;
    }
}
