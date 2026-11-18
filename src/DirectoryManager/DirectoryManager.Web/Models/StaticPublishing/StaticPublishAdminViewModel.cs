using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Models.StaticPublishing
{
    public class StaticPublishAdminViewModel
    {
        public StaticPublishMode Mode { get; set; }

        public bool InternalEndpointEnabled { get; set; }

        public int MaxAttempts { get; set; }

        public string PublicBaseUrl { get; set; } = string.Empty;

        public DateTime? WorkerLastPollUtc { get; set; }

        public List<StaticPublishQueueItem> Waiting { get; set; } = new List<StaticPublishQueueItem>();

        public List<StaticPublishQueueItem> Failing { get; set; } = new List<StaticPublishQueueItem>();

        public StaticPublishQueueItem? FullCrawlRequest { get; set; }

        public StaticPublishLogEntry? LastFullCrawl { get; set; }

        public StaticPublishLogEntry? LastScheduledCrawl { get; set; }

        /// <summary>When a full crawl last reported in (also gate-skipped ones), in-memory.</summary>
        public DateTime? LastCrawlReportUtc { get; set; }

        /// <summary>Pages full crawls changed in the last 24 h that were neither queued nor explained by a
        /// site-wide change marker: changes instant publishing missed.</summary>
        public List<StaticPublishLogEntry> Drift { get; set; } = new List<StaticPublishLogEntry>();

        public StaticPublishLogEntry? LastInstantPublish { get; set; }

        public StaticPublishLogEntry? LastPublishForPath { get; set; }

        public PagedResult<StaticPublishLogEntry> Log { get; set; } = new PagedResult<StaticPublishLogEntry>();

        public string? FilterPath { get; set; }

        public string? FilterResult { get; set; }

        public string? FilterSource { get; set; }
    }
}
