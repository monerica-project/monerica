using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Models.StaticPublishing
{
    /// <summary>What the publisher POSTs to /internal/static-publish/result.</summary>
    public class StaticPublishResultReport
    {
        /// <summary>"instant" or "full-crawl".</summary>
        public string? Source { get; set; }

        /// <summary>The servedUtc of the batch these results belong to.</summary>
        public string? ServedUtc { get; set; }

        public bool DryRun { get; set; }

        public List<StaticPublishResultRow> Results { get; set; } = new List<StaticPublishResultRow>();

        public StaticPublishFullCrawlRow? FullCrawl { get; set; }
    }
}
