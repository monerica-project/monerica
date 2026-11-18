namespace DirectoryManager.TermsWatcher.Watch
{
    public sealed class TermsWatchOptions
    {
        public string Model { get; set; } = "claude-sonnet-5-5";

        public int MaxTokens { get; set; } = 1200;

        // Bill protection: AI calls per calendar month (UTC), across all runs. A call is only made
        // when a listing's policy wording changed since the last check, so most months use a
        // fraction of this. When it is used up, changes are still recorded and shown for a person.
        public int MonthlyAiCap { get; set; } = 120;

        // A listing is looked at again only after this many days (the job itself runs monthly).
        public int MinDaysBetweenChecks { get; set; } = 25;

        public int Concurrency { get; set; } = 4;

        public int MaxListingsPerRun { get; set; } = 2000;

        public int MaxApiAttempts { get; set; } = 3;

        // Sentences sent to the AI per side (added / removed), to keep each call small.
        public int MaxDiffSentences { get; set; } = 25;

        public string UserAgent { get; set; } = "MonericaTermsWatcher/1.0 (+https://monerica.com; monthly check of listed sites' public policy pages)";

        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.anthropic.com/";

        public string TorHost { get; set; } = "127.0.0.1";

        public int TorPort { get; set; } = 9050;
    }
}
