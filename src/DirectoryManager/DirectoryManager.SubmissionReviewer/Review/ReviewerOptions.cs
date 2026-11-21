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

        // Drafts never passed the captcha, so at most this many auto-sent ones may wait for the admin
        // at a time. A flood of abandoned (or scripted) drafts cannot fill the review queue.
        public int PreviewPromoteMaxWaiting { get; set; } = 15;

        public int DossierMaxChars { get; set; } = 6000;

        public string UserAgent { get; set; } = "MonericaSubmissionReviewer/1.0 (+https://monerica.com; reviews directory submissions)";

        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.anthropic.com/";

        public string TorHost { get; set; } = "127.0.0.1";

        public int TorPort { get; set; } = 9050;

        /// <summary>Open pages the plain fetch could not read (JavaScript-built or refusing scripts) in a headless browser.</summary>
        public bool RenderEnabled { get; set; } = true;

        /// <summary>Locked-down user the browser runs as; the runner is skipped where it is not installed.</summary>
        public string RenderRunAsUser { get; set; } = "order-render";

        public string RenderRunnerPath { get; set; } = "/opt/monerica-order-render/run.sh";

        public string SudoPath { get; set; } = "/usr/bin/sudo";

        /// <summary>The runner stops itself after 170 s; wait a little longer than that.</summary>
        public int RenderTimeoutSeconds { get; set; } = 210;
    }
}
