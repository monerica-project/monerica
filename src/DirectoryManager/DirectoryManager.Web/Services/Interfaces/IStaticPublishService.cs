using DirectoryManager.Web.Models.StaticPublishing;

namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>
    /// Queues public pages for instant publishing to the static mirror. Every method is safe to call
    /// from any action: it never throws (a queue problem is logged and the page is caught by the
    /// next full crawl) and does nothing when StaticPublish:Mode is Off.
    /// </summary>
    public interface IStaticPublishService
    {
        StaticPublishMode Mode { get; }

        /// <summary>The pages a listing currently appears on. Call BEFORE an edit and pass the result
        /// to <see cref="EnqueueForEntryAsync"/>, so pages it leaves (old subcategory, old tag, old
        /// country, old key) are republished too.</summary>
        Task<IReadOnlyList<string>> PathsForEntryAsync(int directoryEntryId, CancellationToken ct = default);

        Task EnqueueForEntryAsync(int directoryEntryId, string reason, IEnumerable<string>? pathsBefore = null, CancellationToken ct = default);

        Task EnqueueForReviewAsync(int directoryEntryReviewId, string reason, CancellationToken ct = default);

        Task EnqueueForReplyAsync(int directoryEntryReviewCommentId, string reason, CancellationToken ct = default);

        Task EnqueueAsync(IEnumerable<string> paths, string reason, CancellationToken ct = default);

        /// <summary>For changes shown on every page (header/footer snippets, sponsor ticker, sidebar
        /// categories, deploys) instead of thousands of queue rows. By default this only records a
        /// "site-wide change" marker: the 5-minute sitemap-gated crawl sees these changes anyway, and the
        /// marker tells the drift report that the pages it changes were expected.
        /// <paramref name="runNow"/> = the sitemap gate cannot see this change: crawl at once.</summary>
        Task RequestFullCrawlAsync(string reason, CancellationToken ct = default, bool runNow = false);
    }
}
