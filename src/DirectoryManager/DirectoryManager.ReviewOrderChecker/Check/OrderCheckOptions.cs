namespace DirectoryManager.ReviewOrderChecker.Check
{
    public sealed class OrderCheckOptions
    {
        public string Model { get; set; } = "claude-sonnet-5-5";

        public int MaxTokens { get; set; } = 2500;

        // Bill protection: AI calls per UTC day and reviews per run (the job runs every 10 minutes).
        public int DailyCap { get; set; } = 40;

        public int PerRunCap { get; set; } = 3;

        public int MaxApiAttempts { get; set; } = 4;

        // An order still in progress is looked at again later, up to this many times in total.
        public int MaxChecksPerReview { get; set; } = 4;

        public int RecheckAfterMinutes { get; set; } = 90;

        // Only reviews this recent are picked up automatically (order pages expire).
        public int MaxReviewAgeDays { get; set; } = 21;

        public int PageMaxChars { get; set; } = 9000;

        // The headless browser that opens order pages (installed once on the server).
        public string PythonPath { get; set; } = "/opt/monerica-order-render/venv/bin/python";

        public string BrowsersPath { get; set; } = "/opt/monerica-order-render/browsers";

        public string RenderScriptPath { get; set; } = string.Empty;

        public int RenderTimeoutSeconds { get; set; } = 150;

        // On the server the browser runs as a separate, firewalled user: when RunAsUser is set the
        // job starts RunnerPath through "sudo -n -u RunAsUser" (a sudoers rule allows exactly that
        // one script). Empty = start PythonPath directly as the job's own user (local development).
        public string RunAsUser { get; set; } = string.Empty;

        public string RunnerPath { get; set; } = "/opt/monerica-order-render/run.sh";

        public string SudoPath { get; set; } = "/usr/bin/sudo";

        public string PriceApiBaseUrl { get; set; } = "https://moneromarketcap.com";

        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.anthropic.com/";

        public string TorHost { get; set; } = "127.0.0.1";

        public int TorPort { get; set; } = 9050;
    }
}
