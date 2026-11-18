using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public sealed class StaticPublishRepository : IStaticPublishRepository
    {
        private readonly ApplicationDbContext db;

        public StaticPublishRepository(ApplicationDbContext db)
        {
            this.db = db;
        }

        /// <summary>Wait before retry number <paramref name="attempts"/>+1: 30 s, 1, 2, 4 min, ...</summary>
        public static TimeSpan BackoffAfter(int attempts) =>
            attempts <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(30 * Math.Pow(2, Math.Min(attempts, 10) - 1));

        public async Task EnqueueAsync(IEnumerable<string> paths, string reason, string source, DateTime nowUtc, CancellationToken ct = default)
        {
            var list = paths
                .Where(p => !string.IsNullOrWhiteSpace(p) && p.Length <= StaticPublishConstants.MaxPathLength)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (list.Count == 0)
            {
                return;
            }

            reason = Clip(reason, StaticPublishConstants.MaxReasonLength) ?? string.Empty;
            for (var attempt = 0; ; attempt++)
            {
                var existing = await this.db.StaticPublishQueueItems
                    .Where(q => list.Contains(q.Path))
                    .ToDictionaryAsync(q => q.Path, StringComparer.Ordinal, ct);

                foreach (var path in list)
                {
                    if (existing.TryGetValue(path, out var row))
                    {
                        row.LastRequestedUtc = nowUtc;
                        row.Reason = reason;
                        row.Attempts = 0;
                        row.LastAttemptUtc = null;
                        if (source != StaticPublishConstants.SourceInstant)
                        {
                            row.Source = source; // manual / forced wins over a plain instant request
                        }
                    }
                    else
                    {
                        this.db.StaticPublishQueueItems.Add(new StaticPublishQueueItem
                        {
                            Path = path,
                            RequestedUtc = nowUtc,
                            LastRequestedUtc = nowUtc,
                            Reason = reason,
                            Source = source,
                        });
                    }
                }

                try
                {
                    await this.db.SaveChangesAsync(ct);
                    return;
                }
                catch (DbUpdateException) when (attempt == 0)
                {
                    // Another request inserted one of these paths at the same moment (unique index):
                    // forget our inserts and redo the upsert against what is there now.
                    foreach (var e in this.db.ChangeTracker.Entries<StaticPublishQueueItem>().ToList())
                    {
                        e.State = EntityState.Detached;
                    }
                }
            }
        }

        public async Task<List<StaticPublishQueueItem>> GetPendingAsync(int limit, int maxAttempts, DateTime nowUtc, CancellationToken ct = default)
        {
            var t1 = nowUtc - BackoffAfter(1);
            var t2 = nowUtc - BackoffAfter(2);
            var t3 = nowUtc - BackoffAfter(3);
            var t4 = nowUtc - BackoffAfter(4);
            var tn = nowUtc - BackoffAfter(5);

            return await this.db.StaticPublishQueueItems
                .AsNoTracking()
                .Where(q => q.Path != StaticPublishConstants.FullCrawlPath && q.Attempts < maxAttempts)
                .Where(q => q.Attempts == 0 || q.LastAttemptUtc == null
                    || (q.Attempts == 1 && q.LastAttemptUtc <= t1)
                    || (q.Attempts == 2 && q.LastAttemptUtc <= t2)
                    || (q.Attempts == 3 && q.LastAttemptUtc <= t3)
                    || (q.Attempts == 4 && q.LastAttemptUtc <= t4)
                    || (q.Attempts >= 5 && q.LastAttemptUtc <= tn))
                .OrderBy(q => q.RequestedUtc)
                .ThenBy(q => q.StaticPublishQueueItemId)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync(ct);
        }

        public Task<StaticPublishQueueItem?> GetFullCrawlRequestAsync(CancellationToken ct = default) =>
            this.db.StaticPublishQueueItems.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Path == StaticPublishConstants.FullCrawlPath, ct);

        public async Task<StaticPublishApplySummary> ApplyResultsAsync(
            IEnumerable<StaticPublishResultRow> results,
            DateTime? servedUtc,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            var summary = new StaticPublishApplySummary();
            var rows = results
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Path) && r.Path != StaticPublishConstants.FullCrawlPath)
                .GroupBy(r => r.Path, StringComparer.Ordinal)
                .Select(g => g.Last())
                .ToList();
            if (rows.Count == 0)
            {
                return summary;
            }

            var paths = rows.Select(r => r.Path).ToList();
            var queue = await this.db.StaticPublishQueueItems
                .Where(q => paths.Contains(q.Path))
                .ToDictionaryAsync(q => q.Path, StringComparer.Ordinal, ct);

            foreach (var r in rows)
            {
                if (r.Result == StaticPublishConstants.ResultDeferred)
                {
                    continue; // the app was down: not an attempt, not logged
                }

                queue.TryGetValue(r.Path, out var item);
                var done = StaticPublishConstants.IsDone(r.Result);
                var result = done ? r.Result : StaticPublishConstants.ResultFailed;

                if (item != null)
                {
                    if (done)
                    {
                        if (servedUtc.HasValue && item.LastRequestedUtc > servedUtc.Value)
                        {
                            // Changed again after this batch was handed out: publish it once more.
                            item.Attempts = 0;
                            item.LastAttemptUtc = null;
                            summary.KeptForRepublish++;
                        }
                        else
                        {
                            this.db.StaticPublishQueueItems.Remove(item);
                            summary.Removed++;
                        }
                    }
                    else
                    {
                        item.Attempts++;
                        item.LastAttemptUtc = nowUtc;
                        item.LastError = Clip(r.Error ?? "failed", StaticPublishConstants.MaxErrorLength);
                        summary.Failed++;
                    }
                }

                this.db.StaticPublishLogEntries.Add(new StaticPublishLogEntry
                {
                    Path = Clip(r.Path, StaticPublishConstants.MaxPathLength)!,
                    Reason = item?.Reason,
                    RequestedUtc = item?.RequestedUtc,
                    PublishedUtc = nowUtc,
                    DurationMs = (int)Math.Clamp(r.DurationMs, 0, int.MaxValue),
                    Result = result,
                    Error = Clip(r.Error, StaticPublishConstants.MaxErrorLength),
                    Source = item?.Source ?? StaticPublishConstants.SourceInstant,
                });
                summary.Logged++;
            }

            await this.db.SaveChangesAsync(ct);
            return summary;
        }

        public async Task RecordFullCrawlAsync(StaticPublishFullCrawlRow crawl, DateTime? servedUtc, DateTime nowUtc, int maxPageRows, CancellationToken ct = default)
        {
            var request = await this.db.StaticPublishQueueItems
                .FirstOrDefaultAsync(q => q.Path == StaticPublishConstants.FullCrawlPath, ct);

            // When the crawl began on the app's clock (the publisher asks the queue first; fall back to
            // now minus its duration).
            var startedUtc = servedUtc ?? nowUtc.AddMilliseconds(-Math.Max(0, crawl.DurationMs));
            var requestCovered = request != null && request.LastRequestedUtc <= startedUtc;

            var requested = string.Equals(crawl.Trigger, "requested", StringComparison.OrdinalIgnoreCase);
            var changed = crawl.Uploaded + crawl.Deleted;

            // A gated crawl that found nothing to do is not worth a log row every 5 minutes.
            var logIt = !(crawl.Ok && crawl.SkippedByGate && changed == 0 && !requested);
            if (logIt)
            {
                var details = crawl.SkippedByGate
                    ? $"{crawl.PagesInSitemap} pages in sitemap; crawl skipped (nothing changed); {changed} file(s) updated"
                    : $"{crawl.PagesInSitemap} pages crawled; {crawl.Uploaded} uploaded, {crawl.Deleted} deleted, {crawl.Failed} failed";
                if (!string.IsNullOrWhiteSpace(crawl.Error))
                {
                    details += " — " + crawl.Error;
                }

                var reason = requested
                    ? "Full crawl (requested" + (string.IsNullOrWhiteSpace(crawl.Reason) ? ")" : ": " + crawl.Reason + ")")
                    : "Full crawl (scheduled)";

                this.db.StaticPublishLogEntries.Add(new StaticPublishLogEntry
                {
                    Path = StaticPublishConstants.FullCrawlPath,
                    Reason = Clip(reason, StaticPublishConstants.MaxReasonLength),
                    RequestedUtc = requestCovered ? request!.RequestedUtc : null,
                    PublishedUtc = nowUtc,
                    DurationMs = (int)Math.Clamp(crawl.DurationMs, 0, int.MaxValue),
                    Result = !crawl.Ok && !crawl.SkippedByGate
                        ? StaticPublishConstants.ResultFailed
                        : changed > 0 ? StaticPublishConstants.ResultPublished : StaticPublishConstants.ResultUnchanged,
                    Error = Clip(details, StaticPublishConstants.MaxErrorLength),
                    Source = StaticPublishConstants.SourceFullCrawl,
                });

                // Label each changed page: still waiting in the queue (the crawl just got there first),
                // explained by a site-wide change marker, or real drift (no enqueue call for that change).
                var pages = crawl.ChangedPaths.Select(p => (Path: p, Result: StaticPublishConstants.ResultPublished))
                    .Concat(crawl.DeletedPaths.Select(p => (Path: p, Result: StaticPublishConstants.ResultRemoved)))
                    .Where(p => !string.IsNullOrWhiteSpace(p.Path) && p.Path.Length <= StaticPublishConstants.MaxPathLength)
                    .Take(Math.Max(0, maxPageRows))
                    .ToList();
                var pagePaths = pages.Select(p => p.Path).ToList();
                var queued = (await this.db.StaticPublishQueueItems.AsNoTracking()
                        .Where(q => pagePaths.Contains(q.Path))
                        .Select(q => q.Path)
                        .ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
                var expected = request != null && (requestCovered || requested)
                    ? Clip(StaticPublishConstants.ExpectedReasonPrefix + (request.Reason ?? "site-wide change") + ")", StaticPublishConstants.MaxReasonLength)
                    : requested ? StaticPublishConstants.ExpectedReasonPrefix + "requested full crawl)" : null;

                foreach (var (path, result) in pages)
                {
                    this.db.StaticPublishLogEntries.Add(new StaticPublishLogEntry
                    {
                        Path = path,
                        Reason = queued.Contains(path) ? StaticPublishConstants.QueuedReason : expected ?? StaticPublishConstants.DriftReason,
                        PublishedUtc = nowUtc,
                        Result = result,
                        Source = StaticPublishConstants.SourceFullCrawl,
                    });
                }
            }

            // Any successful crawl that began after the "*" row was (last) written covers it: clear it.
            if (request != null && crawl.Ok && requestCovered)
            {
                this.db.StaticPublishQueueItems.Remove(request);
            }
            else if (requested && request != null && !crawl.Ok)
            {
                request.Attempts++;
                request.LastAttemptUtc = nowUtc;
                request.LastError = Clip(crawl.Error ?? "full crawl failed", StaticPublishConstants.MaxErrorLength);
            }

            await this.db.SaveChangesAsync(ct);
        }

        public Task<List<StaticPublishLogEntry>> ListDriftAsync(DateTime sinceUtc, int max, CancellationToken ct = default) =>
            this.db.StaticPublishLogEntries.AsNoTracking()
                .Where(x => x.Source == StaticPublishConstants.SourceFullCrawl
                    && x.Reason == StaticPublishConstants.DriftReason
                    && x.PublishedUtc >= sinceUtc)
                .OrderByDescending(x => x.PublishedUtc)
                .ThenBy(x => x.Path)
                .Take(Math.Clamp(max, 1, 2000))
                .ToListAsync(ct);

        public Task<List<StaticPublishQueueItem>> ListQueueAsync(CancellationToken ct = default) =>
            this.db.StaticPublishQueueItems.AsNoTracking()
                .OrderBy(q => q.RequestedUtc)
                .ThenBy(q => q.StaticPublishQueueItemId)
                .Take(1000)
                .ToListAsync(ct);

        public async Task<PagedResult<StaticPublishLogEntry>> ListLogAsync(StaticPublishLogFilter filter, CancellationToken ct = default)
        {
            var page = Math.Max(1, filter.Page);
            var size = Math.Clamp(filter.PageSize, 1, 200);
            IQueryable<StaticPublishLogEntry> q = this.db.StaticPublishLogEntries.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Path))
            {
                var p = filter.Path.Trim();
                if (p.Length > 1 && p.EndsWith('*'))
                {
                    var prefix = p[..^1];
                    q = q.Where(x => x.Path.StartsWith(prefix));
                }
                else
                {
                    q = q.Where(x => x.Path == p);
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Result))
            {
                q = q.Where(x => x.Result == filter.Result);
            }

            if (!string.IsNullOrWhiteSpace(filter.Source))
            {
                q = q.Where(x => x.Source == filter.Source);
            }

            var total = await q.CountAsync(ct);
            var items = await q
                .OrderByDescending(x => x.PublishedUtc)
                .ThenByDescending(x => x.StaticPublishLogEntryId)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return new PagedResult<StaticPublishLogEntry> { Items = items, TotalCount = total, Page = page, PageSize = size };
        }

        public Task<StaticPublishLogEntry?> GetLastPublishForPathAsync(string path, CancellationToken ct = default) =>
            this.db.StaticPublishLogEntries.AsNoTracking()
                .Where(x => x.Path == path
                    && (x.Result == StaticPublishConstants.ResultPublished
                        || x.Result == StaticPublishConstants.ResultUnchanged
                        || x.Result == StaticPublishConstants.ResultWouldPublish))
                .OrderByDescending(x => x.PublishedUtc)
                .ThenByDescending(x => x.StaticPublishLogEntryId)
                .FirstOrDefaultAsync(ct);

        public Task<StaticPublishLogEntry?> GetLastFullCrawlAsync(bool scheduledOnly = false, CancellationToken ct = default)
        {
            var q = this.db.StaticPublishLogEntries.AsNoTracking()
                .Where(x => x.Path == StaticPublishConstants.FullCrawlPath && x.Source == StaticPublishConstants.SourceFullCrawl);
            if (scheduledOnly)
            {
                q = q.Where(x => x.Reason == "Full crawl (scheduled)");
            }

            return q.OrderByDescending(x => x.PublishedUtc).ThenByDescending(x => x.StaticPublishLogEntryId).FirstOrDefaultAsync(ct);
        }

        public Task<List<StaticPublishLogEntry>> ListFullCrawlPagesAsync(DateTime crawlPublishedUtc, int max, CancellationToken ct = default) =>
            this.db.StaticPublishLogEntries.AsNoTracking()
                .Where(x => x.Source == StaticPublishConstants.SourceFullCrawl
                    && x.Path != StaticPublishConstants.FullCrawlPath
                    && x.PublishedUtc == crawlPublishedUtc)
                .OrderBy(x => x.Path)
                .Take(Math.Clamp(max, 1, 2000))
                .ToListAsync(ct);

        public Task<StaticPublishLogEntry?> GetLastInstantPublishAsync(CancellationToken ct = default) =>
            this.db.StaticPublishLogEntries.AsNoTracking()
                .Where(x => x.Source != StaticPublishConstants.SourceFullCrawl
                    && (x.Result == StaticPublishConstants.ResultPublished || x.Result == StaticPublishConstants.ResultRemoved))
                .OrderByDescending(x => x.PublishedUtc)
                .ThenByDescending(x => x.StaticPublishLogEntryId)
                .FirstOrDefaultAsync(ct);

        public async Task<int> RetryFailedAsync(CancellationToken ct = default)
        {
            var rows = await this.db.StaticPublishQueueItems.Where(q => q.Attempts > 0).ToListAsync(ct);
            foreach (var r in rows)
            {
                r.Attempts = 0;
                r.LastAttemptUtc = null;
            }

            await this.db.SaveChangesAsync(ct);
            return rows.Count;
        }

        public async Task<int> PruneLogAsync(DateTime olderThanUtc, CancellationToken ct = default)
        {
            if (this.db.Database.IsRelational())
            {
                return await this.db.StaticPublishLogEntries.Where(x => x.PublishedUtc < olderThanUtc).ExecuteDeleteAsync(ct);
            }

            var old = await this.db.StaticPublishLogEntries.Where(x => x.PublishedUtc < olderThanUtc).ToListAsync(ct);
            this.db.StaticPublishLogEntries.RemoveRange(old);
            await this.db.SaveChangesAsync(ct);
            return old.Count;
        }

        private static string? Clip(string? s, int max) =>
            s == null ? null : (s.Length <= max ? s : s[..max]);
    }
}
