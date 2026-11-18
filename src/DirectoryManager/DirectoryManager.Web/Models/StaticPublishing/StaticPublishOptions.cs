namespace DirectoryManager.Web.Models.StaticPublishing
{
    /// <summary>Config section "StaticPublish". The secret lives only in the server's untracked
    /// appsettings.Production.json.</summary>
    public class StaticPublishOptions
    {
        public const string SectionName = "StaticPublish";

        public StaticPublishMode Mode { get; set; } = StaticPublishMode.Off;

        /// <summary>Loopback-only port the internal publisher endpoints listen on (0 = disabled).
        /// nginx and Tor never proxy to it, so nothing from outside can reach it.</summary>
        public int InternalPort { get; set; }

        /// <summary>Shared secret the publisher sends in <see cref="SecretHeaderName"/>.</summary>
        public string InternalSecret { get; set; } = string.Empty;

        public string SecretHeaderName { get; set; } = "X-Static-Publish-Key";

        /// <summary>Rows with this many failed attempts are parked until "Retry failed".</summary>
        public int MaxAttempts { get; set; } = 5;

        public int LogRetentionDays { get; set; } = 30;

        /// <summary>Most per-page rows logged for one full crawl (a deploy changes every page).</summary>
        public int MaxFullCrawlPageRows { get; set; } = 2500;

        /// <summary>Public domain of the mirror, for "view on the live site" links on the admin page.</summary>
        public string PublicBaseUrl { get; set; } = "https://monerica.com";

        /// <summary>The internal endpoints are usable: a port and a non-trivial secret are configured.</summary>
        public bool InternalEndpointEnabled => this.InternalPort > 0 && (this.InternalSecret?.Length ?? 0) >= 24;
    }
}
