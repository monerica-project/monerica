using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    /// <summary>The publish-on-change queue: upsert/dedupe, hand-out rules and result handling.</summary>
    public class StaticPublishRepositoryTests
    {
        private static readonly DateTime T0 = new (2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Enqueue_SamePathTwice_KeepsOneRow_WithTheEarliestRequestTime()
        {
            using var db = NewContext();
            var repo = new StaticPublishRepository(db);

            await repo.EnqueueAsync(new[] { "/site/a", "/", "/site/a" }, "first", StaticPublishConstants.SourceInstant, T0);
            await repo.EnqueueAsync(new[] { "/site/a" }, "second", StaticPublishConstants.SourceInstant, T0.AddMinutes(1));

            var rows = await db.StaticPublishQueueItems.OrderBy(q => q.Path).ToListAsync();
            Assert.Equal(new[] { "/", "/site/a" }, rows.Select(r => r.Path));
            var a = rows.Single(r => r.Path == "/site/a");
            Assert.Equal(T0, a.RequestedUtc);
            Assert.Equal(T0.AddMinutes(1), a.LastRequestedUtc);
            Assert.Equal("second", a.Reason);
        }

        [Fact]
        public async Task Enqueue_AgainAfterFailures_ResetsAttempts_AndManualWins()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(new StaticPublishQueueItem { Path = "/x", RequestedUtc = T0, LastRequestedUtc = T0, Attempts = 5, LastAttemptUtc = T0, LastError = "boom" });
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.EnqueueAsync(new[] { "/x" }, "manual: again", StaticPublishConstants.SourceManual, T0.AddHours(1));

            var row = await db.StaticPublishQueueItems.SingleAsync();
            Assert.Equal(0, row.Attempts);
            Assert.Null(row.LastAttemptUtc);
            Assert.Equal(StaticPublishConstants.SourceManual, row.Source);
        }

        [Fact]
        public async Task Pending_SkipsTheFullCrawlRow_ParkedRows_AndRowsInBackoff()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.AddRange(
                Row("/new", T0),
                Row(StaticPublishConstants.FullCrawlPath, T0),
                Row("/parked", T0, attempts: 5, last: T0.AddHours(-1)),
                Row("/backoff", T0, attempts: 2, last: T0.AddSeconds(-30)),     // needs 60 s
                Row("/retry", T0.AddSeconds(-5), attempts: 1, last: T0.AddSeconds(-31))); // needs 30 s
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            var pending = await repo.GetPendingAsync(50, maxAttempts: 5, T0);

            Assert.Equal(new[] { "/retry", "/new" }, pending.Select(p => p.Path));
            Assert.NotNull(await repo.GetFullCrawlRequestAsync());
        }

        [Fact]
        public async Task Results_SuccessDeletesTheRow_AndLogs_FailureCountsAnAttempt_DeferredIsIgnored()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.AddRange(Row("/ok", T0, reason: "listing edited: A"), Row("/same", T0), Row("/bad", T0), Row("/later", T0));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            var summary = await repo.ApplyResultsAsync(
                new[]
                {
                    new StaticPublishResultRow { Path = "/ok", Result = StaticPublishConstants.ResultPublished, DurationMs = 1200 },
                    new StaticPublishResultRow { Path = "/same", Result = StaticPublishConstants.ResultUnchanged },
                    new StaticPublishResultRow { Path = "/bad", Result = StaticPublishConstants.ResultFailed, Error = "HTTP 500" },
                    new StaticPublishResultRow { Path = "/later", Result = StaticPublishConstants.ResultDeferred },
                },
                servedUtc: T0.AddSeconds(1),
                nowUtc: T0.AddSeconds(5));

            Assert.Equal(2, summary.Removed);
            Assert.Equal(1, summary.Failed);
            var left = await db.StaticPublishQueueItems.OrderBy(q => q.Path).ToListAsync();
            Assert.Equal(new[] { "/bad", "/later" }, left.Select(q => q.Path));
            var bad = left.Single(q => q.Path == "/bad");
            Assert.Equal(1, bad.Attempts);
            Assert.Equal("HTTP 500", bad.LastError);
            Assert.Equal(T0.AddSeconds(5), bad.LastAttemptUtc);
            Assert.Equal(0, left.Single(q => q.Path == "/later").Attempts);

            var log = await db.StaticPublishLogEntries.OrderBy(l => l.Path).ToListAsync();
            Assert.Equal(new[] { "/bad", "/ok", "/same" }, log.Select(l => l.Path));
            var ok = log.Single(l => l.Path == "/ok");
            Assert.Equal("listing edited: A", ok.Reason);
            Assert.Equal(T0, ok.RequestedUtc);
            Assert.Equal(T0.AddSeconds(5), ok.PublishedUtc);
            Assert.Equal(1200, ok.DurationMs);
            Assert.Equal(StaticPublishConstants.SourceInstant, ok.Source);
        }

        [Fact]
        public async Task Results_PageChangedAgainAfterHandOut_StaysQueued()
        {
            using var db = NewContext();
            var row = Row("/site/a", T0);
            row.LastRequestedUtc = T0.AddSeconds(10); // edited again while being published
            db.StaticPublishQueueItems.Add(row);
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            var summary = await repo.ApplyResultsAsync(
                new[] { new StaticPublishResultRow { Path = "/site/a", Result = StaticPublishConstants.ResultPublished } },
                servedUtc: T0.AddSeconds(5),
                nowUtc: T0.AddSeconds(12));

            Assert.Equal(1, summary.KeptForRepublish);
            Assert.Single(db.StaticPublishQueueItems);
            Assert.Single(db.StaticPublishLogEntries);
        }

        [Fact]
        public async Task DryRunResults_FinishTheRow_AndAreLoggedAsWouldPublish()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row("/a", T0));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.ApplyResultsAsync(
                new[] { new StaticPublishResultRow { Path = "/a", Result = StaticPublishConstants.ResultWouldPublish } },
                T0.AddSeconds(1),
                T0.AddSeconds(2));

            Assert.Empty(db.StaticPublishQueueItems);
            Assert.Equal(StaticPublishConstants.ResultWouldPublish, db.StaticPublishLogEntries.Single().Result);
        }

        [Fact]
        public async Task FullCrawl_LogsASummaryAndChangedPages_AndClearsTheRequest()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row(StaticPublishConstants.FullCrawlPath, T0, reason: "deploy"));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.RecordFullCrawlAsync(
                new StaticPublishFullCrawlRow
                {
                    Trigger = "requested",
                    Reason = "deploy",
                    Ok = true,
                    PagesInSitemap = 1700,
                    Uploaded = 2,
                    Deleted = 1,
                    ChangedPaths = new List<string> { "/", "/site/a" },
                    DeletedPaths = new List<string> { "/site/old" },
                },
                servedUtc: T0.AddSeconds(1),
                nowUtc: T0.AddMinutes(4),
                maxPageRows: 100);

            Assert.Empty(db.StaticPublishQueueItems);
            var summary = await repo.GetLastFullCrawlAsync();
            Assert.NotNull(summary);
            Assert.Equal(StaticPublishConstants.ResultPublished, summary!.Result);
            Assert.Contains("2 uploaded, 1 deleted", summary.Error);
            var pages = await repo.ListFullCrawlPagesAsync(summary.PublishedUtc, 50);
            Assert.Equal(new[] { "/", "/site/a", "/site/old" }, pages.Select(p => p.Path));
            Assert.Equal(StaticPublishConstants.ResultRemoved, pages.Single(p => p.Path == "/site/old").Result);
            Assert.Null(await repo.GetLastFullCrawlAsync(scheduledOnly: true));
        }

        [Fact]
        public async Task ScheduledCrawl_LabelsQueuedExpectedAndDriftPages_SoOnlyRealMissesShowAsDrift()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row("/site/waiting", T0));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.RecordFullCrawlAsync(
                new StaticPublishFullCrawlRow { Trigger = "scheduled", Ok = true, Uploaded = 2, ChangedPaths = new List<string> { "/site/waiting", "/site/missed" } },
                servedUtc: T0.AddSeconds(5),
                nowUtc: T0.AddMinutes(3),
                maxPageRows: 100);

            var drift = await repo.ListDriftAsync(T0.AddHours(-1), 50);
            Assert.Equal(new[] { "/site/missed" }, drift.Select(d => d.Path));
            Assert.Equal(StaticPublishConstants.QueuedReason, db.StaticPublishLogEntries.Single(l => l.Path == "/site/waiting").Reason);
            Assert.Single(db.StaticPublishQueueItems); // the queued page is still published by the instant path
        }

        [Fact]
        public async Task SiteWideMarker_IsClearedByTheNextGatedCrawl_AndItsPagesAreExpectedNotDrift()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row(StaticPublishConstants.FullCrawlPath, T0, reason: "content snippet edited: FooterHtml"));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.RecordFullCrawlAsync(
                new StaticPublishFullCrawlRow { Trigger = "scheduled", Ok = true, Uploaded = 2, ChangedPaths = new List<string> { "/", "/faq" } },
                servedUtc: T0.AddSeconds(30),
                nowUtc: T0.AddMinutes(3),
                maxPageRows: 100);

            Assert.Empty(db.StaticPublishQueueItems);
            Assert.Empty(await repo.ListDriftAsync(T0.AddHours(-1), 50));
            Assert.All(db.StaticPublishLogEntries.Where(l => l.Path != StaticPublishConstants.FullCrawlPath), l => Assert.StartsWith(StaticPublishConstants.ExpectedReasonPrefix, l.Reason));
        }

        [Fact]
        public async Task MarkerWrittenAfterTheCrawlBegan_IsKept_AndGateSkippedCrawlsAreNotLogged()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row(StaticPublishConstants.FullCrawlPath, T0.AddMinutes(1), reason: "deploy"));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.RecordFullCrawlAsync(
                new StaticPublishFullCrawlRow { Trigger = "scheduled", Ok = true, SkippedByGate = true },
                servedUtc: T0,
                nowUtc: T0.AddMinutes(2),
                maxPageRows: 100);

            Assert.Single(db.StaticPublishQueueItems);
            Assert.Empty(db.StaticPublishLogEntries);
        }

        [Fact]
        public async Task ForcedRequest_WinsOverAPlainMarker_AndOnlyForcedOnesAskForACrawlNow()
        {
            using var db = NewContext();
            var repo = new StaticPublishRepository(db);

            await repo.EnqueueAsync(new[] { StaticPublishConstants.FullCrawlPath }, "deploy", StaticPublishConstants.SourceInstant, T0);
            Assert.False(StaticPublishConstants.IsForcedCrawlRequest(await repo.GetFullCrawlRequestAsync()));

            await repo.EnqueueAsync(new[] { StaticPublishConstants.FullCrawlPath }, "manual", StaticPublishConstants.SourceForced, T0.AddSeconds(1));
            await repo.EnqueueAsync(new[] { StaticPublishConstants.FullCrawlPath }, "snippet", StaticPublishConstants.SourceInstant, T0.AddSeconds(2));
            Assert.True(StaticPublishConstants.IsForcedCrawlRequest(await repo.GetFullCrawlRequestAsync()));
        }

        [Fact]
        public async Task FailedRequestedCrawl_KeepsTheRequest_WithAnAttempt()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row(StaticPublishConstants.FullCrawlPath, T0));
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            await repo.RecordFullCrawlAsync(
                new StaticPublishFullCrawlRow { Trigger = "requested", Ok = false, Error = "sitemap 503" },
                T0,
                T0.AddMinutes(1),
                100);

            var row = await db.StaticPublishQueueItems.SingleAsync();
            Assert.Equal(1, row.Attempts);
            Assert.Equal(StaticPublishConstants.ResultFailed, (await repo.GetLastFullCrawlAsync())!.Result);
        }

        [Fact]
        public async Task RetryFailed_AndPrune()
        {
            using var db = NewContext();
            db.StaticPublishQueueItems.Add(Row("/x", T0, attempts: 5, last: T0));
            db.StaticPublishLogEntries.AddRange(
                new StaticPublishLogEntry { Path = "/old", PublishedUtc = T0.AddDays(-31), Result = "Published", Source = "instant" },
                new StaticPublishLogEntry { Path = "/new", PublishedUtc = T0.AddDays(-1), Result = "Published", Source = "instant" });
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            Assert.Equal(1, await repo.RetryFailedAsync());
            Assert.Equal(0, (await db.StaticPublishQueueItems.SingleAsync()).Attempts);
            Assert.Equal(1, await repo.PruneLogAsync(T0.AddDays(-30)));
            Assert.Equal("/new", (await db.StaticPublishLogEntries.SingleAsync()).Path);
        }

        [Fact]
        public async Task Log_FiltersByPathPrefixAndResult()
        {
            using var db = NewContext();
            db.StaticPublishLogEntries.AddRange(
                new StaticPublishLogEntry { Path = "/site/a", PublishedUtc = T0, Result = "Published", Source = "instant" },
                new StaticPublishLogEntry { Path = "/site/b", PublishedUtc = T0.AddSeconds(1), Result = "Failed", Source = "instant" },
                new StaticPublishLogEntry { Path = "/", PublishedUtc = T0.AddSeconds(2), Result = "Published", Source = "manual" });
            await db.SaveChangesAsync();
            var repo = new StaticPublishRepository(db);

            var prefix = await repo.ListLogAsync(new StaticPublishLogFilter { Path = "/site/*" });
            Assert.Equal(new[] { "/site/b", "/site/a" }, prefix.Items.Select(i => i.Path));

            var failed = await repo.ListLogAsync(new StaticPublishLogFilter { Result = "Failed" });
            Assert.Equal("/site/b", failed.Items.Single().Path);

            Assert.Equal("/site/a", (await repo.GetLastPublishForPathAsync("/site/a"))!.Path);
            Assert.Null(await repo.GetLastPublishForPathAsync("/site/b"));
        }

        private static StaticPublishQueueItem Row(string path, DateTime requested, int attempts = 0, DateTime? last = null, string? reason = null) =>
            new StaticPublishQueueItem
            {
                Path = path,
                RequestedUtc = requested,
                LastRequestedUtc = requested,
                Attempts = attempts,
                LastAttemptUtc = last,
                Reason = reason,
            };

        private static ApplicationDbContext NewContext() =>
            new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"static-publish-{Guid.NewGuid():N}").Options);
    }
}
