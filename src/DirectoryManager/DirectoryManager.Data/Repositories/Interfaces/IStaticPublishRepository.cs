using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    /// <summary>The static-mirror publish queue and its 30-day log.</summary>
    public interface IStaticPublishRepository
    {
        /// <summary>Upsert: each path at most once; keeps the earliest RequestedUtc, moves LastRequestedUtc,
        /// replaces the reason, and resets attempts so a failing page is retried after a new change.</summary>
        Task EnqueueAsync(IEnumerable<string> paths, string reason, string source, DateTime nowUtc, CancellationToken ct = default);

        /// <summary>Rows ready to publish (oldest first): not the full-crawl row, fewer than
        /// <paramref name="maxAttempts"/> attempts, and past their retry back-off.</summary>
        Task<List<StaticPublishQueueItem>> GetPendingAsync(int limit, int maxAttempts, DateTime nowUtc, CancellationToken ct = default);

        Task<StaticPublishQueueItem?> GetFullCrawlRequestAsync(CancellationToken ct = default);

        /// <summary>Applies instant results: done → delete row (unless re-requested after
        /// <paramref name="servedUtc"/>) + log; failed → attempts+1 + log; deferred → ignored.</summary>
        Task<StaticPublishApplySummary> ApplyResultsAsync(
            IEnumerable<StaticPublishResultRow> results,
            DateTime? servedUtc,
            DateTime nowUtc,
            CancellationToken ct = default);

        /// <summary>Logs a full crawl (summary row "*" + one labeled row per changed/deleted page; a gated
        /// crawl that changed nothing is not logged) and clears the "*" row if the crawl began after it.</summary>
        Task RecordFullCrawlAsync(StaticPublishFullCrawlRow crawl, DateTime? servedUtc, DateTime nowUtc, int maxPageRows, CancellationToken ct = default);

        Task<List<StaticPublishQueueItem>> ListQueueAsync(CancellationToken ct = default);

        Task<PagedResult<StaticPublishLogEntry>> ListLogAsync(StaticPublishLogFilter filter, CancellationToken ct = default);

        /// <summary>Newest successful (or dry-run) publish result for one path.</summary>
        Task<StaticPublishLogEntry?> GetLastPublishForPathAsync(string path, CancellationToken ct = default);

        /// <summary>Newest full-crawl summary row (optionally only scheduled ones).</summary>
        Task<StaticPublishLogEntry?> GetLastFullCrawlAsync(bool scheduledOnly = false, CancellationToken ct = default);

        /// <summary>Pages full crawls changed since <paramref name="sinceUtc"/> that no instant enqueue or
        /// site-wide marker explains (newest first).</summary>
        Task<List<StaticPublishLogEntry>> ListDriftAsync(DateTime sinceUtc, int max, CancellationToken ct = default);

        /// <summary>Pages a full crawl changed (logged with that crawl's summary time).</summary>
        Task<List<StaticPublishLogEntry>> ListFullCrawlPagesAsync(DateTime crawlPublishedUtc, int max, CancellationToken ct = default);

        /// <summary>Newest instant/manual result that actually uploaded something.</summary>
        Task<StaticPublishLogEntry?> GetLastInstantPublishAsync(CancellationToken ct = default);

        /// <summary>Clears attempts on failing rows so they are tried again. Returns how many.</summary>
        Task<int> RetryFailedAsync(CancellationToken ct = default);

        Task<int> PruneLogAsync(DateTime olderThanUtc, CancellationToken ct = default);
    }
}
