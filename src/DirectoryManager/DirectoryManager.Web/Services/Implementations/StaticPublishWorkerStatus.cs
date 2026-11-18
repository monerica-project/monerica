namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>In-memory heartbeat of the publisher worker (when it last asked for work), for the admin page.</summary>
    public static class StaticPublishWorkerStatus
    {
        private static long lastPollTicks;

        public static DateTime? LastPollUtc
        {
            get
            {
                var t = Interlocked.Read(ref lastPollTicks);
                return t == 0 ? null : new DateTime(t, DateTimeKind.Utc);
            }
        }

        public static void MarkPolled(DateTime utc) => Interlocked.Exchange(ref lastPollTicks, utc.Ticks);

        private static long lastCrawlTicks;

        /// <summary>When a full crawl (even one the sitemap gate skipped) last reported in.</summary>
        public static DateTime? LastCrawlReportUtc
        {
            get
            {
                var t = Interlocked.Read(ref lastCrawlTicks);
                return t == 0 ? null : new DateTime(t, DateTimeKind.Utc);
            }
        }

        public static void MarkCrawlReported(DateTime utc) => Interlocked.Exchange(ref lastCrawlTicks, utc.Ticks);
    }
}
