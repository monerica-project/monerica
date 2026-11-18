namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>A full crawl's summary as reported by the publisher.</summary>
    public class StaticPublishFullCrawlRow
    {
        public string Trigger { get; set; } = "scheduled";

        public string? Reason { get; set; }

        public bool SkippedByGate { get; set; }

        public int PagesInSitemap { get; set; }

        public int Uploaded { get; set; }

        public int Deleted { get; set; }

        public int Failed { get; set; }

        public bool Ok { get; set; }

        public string? Error { get; set; }

        public long DurationMs { get; set; }

        public List<string> ChangedPaths { get; set; } = new List<string>();

        public List<string> DeletedPaths { get; set; } = new List<string>();
    }
}
