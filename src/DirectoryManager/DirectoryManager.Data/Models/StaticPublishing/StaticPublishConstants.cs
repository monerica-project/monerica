namespace DirectoryManager.Data.Models.StaticPublishing
{
    public static class StaticPublishConstants
    {
        public const int MaxPathLength = 512;
        public const int MaxReasonLength = 300;
        public const int MaxErrorLength = 1000;

        /// <summary>Queue path meaning "run a full crawl"; log path of a full-crawl summary row.</summary>
        public const string FullCrawlPath = "*";

        public const string SourceInstant = "instant";
        public const string SourceManual = "manual";
        public const string SourceFullCrawl = "full-crawl";

        /// <summary>A "*" row with this source asks the publisher to crawl NOW (admin button, or a
        /// change the sitemap gate cannot see). A "*" row with <see cref="SourceInstant"/> is only a
        /// marker "a site-wide change happened": the next 5-minute gated crawl covers it, and the pages
        /// it changes are labeled expected instead of drift.</summary>
        public const string SourceForced = "forced";

        public const string DriftReason = "changed in a full crawl, not queued for instant publishing";
        public const string QueuedReason = "changed in a full crawl (was waiting in the instant queue)";
        public const string ExpectedReasonPrefix = "changed in a full crawl (expected: ";

        public static bool IsForcedCrawlRequest(StaticPublishQueueItem? row) =>
            row != null && row.Path == FullCrawlPath && row.Source != SourceInstant;

        public const string ResultPublished = "Published";
        public const string ResultUnchanged = "Unchanged";
        public const string ResultFailed = "Failed";
        public const string ResultWouldPublish = "WouldPublish";
        public const string ResultRemoved = "Removed";
        public const string ResultWouldRemove = "WouldRemove";
        public const string ResultDeferred = "Deferred";

        /// <summary>Results that finish a queue row (the row is deleted).</summary>
        public static readonly IReadOnlyCollection<string> DoneResults = new[]
        {
            ResultPublished, ResultUnchanged, ResultWouldPublish, ResultRemoved, ResultWouldRemove,
        };

        public static bool IsDone(string? result) => result != null && DoneResults.Contains(result);
    }
}
