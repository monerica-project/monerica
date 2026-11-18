using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.TermsWatcher.Watch;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DirectoryManager.TermsWatcher.Tests
{
    public class TermsWatchTests
    {
        private const string HomeV1 = "<html><body><p>Fast swaps for everyone.</p><p>We never ask for KYC or identity documents.</p><p>Our fee is 1% on every swap.</p><p>Copyright 2026 ExampleSwap.</p><a href=\"/terms\">Terms</a></body></html>";
        private const string TermsV1 = "<html><body><p>Residents of North Korea and Iran are not permitted to use the service.</p><p>VPN use is allowed.</p></body></html>";

        [Fact]
        public void RelevantSentences_KeepPolicyText_DropMarketing()
        {
            var s = TermsSignalText.RelevantSentences("Fast swaps for everyone. We never ask for KYC or identity documents. Our fee is 1% on every swap. Enjoy the ride with us today.");
            Assert.Equal(2, s.Count);
            Assert.Contains(s, x => x.Contains("KYC"));
            Assert.Contains(s, x => x.Contains("fee"));
        }

        [Fact]
        public void Fingerprint_IgnoresDatesYearsCountersAndOrder()
        {
            var a = TermsSignalText.ComparisonSet(new[] { "Our fee is 1% on every swap.", "Terms last updated 12/03/2026: KYC is never required.", "Total swaps processed: 1,204,551 with no KYC." });
            var b = TermsSignalText.ComparisonSet(new[] { "Total swaps processed: 1,209,812 with no KYC.", "Terms last updated 01/04/2027: KYC is never required.", "Our  fee is 1% on every swap!" });
            Assert.Equal(TermsSignalText.Fingerprint(a), TermsSignalText.Fingerprint(b));

            var c = TermsSignalText.ComparisonSet(new[] { "Our fee is 2% on every swap.", "Terms last updated 12/03/2026: KYC is never required.", "Total swaps processed: 1,204,551 with no KYC." });
            Assert.NotEqual(TermsSignalText.Fingerprint(a), TermsSignalText.Fingerprint(c));
        }

        [Fact]
        public async Task FirstRunIsABaseline_SecondRunUnchanged_NoAiCallsEitherTime()
        {
            using var db = NewContext();
            Seed(db);
            var site = new FakeSite { ["https://example-swap.test/"] = HomeV1, ["https://example-swap.test/terms"] = TermsV1 };
            var ai = new FakeAi(needsUpdate: true);

            var first = await Runner(db, site, ai).RunAsync(default);
            Assert.Equal(1, first.Baselines);
            Assert.Equal(0, first.AiCalls);
            var snap = await db.ListingTermsSnapshots.SingleAsync();
            Assert.Equal(TermsWatchResult.Baseline, snap.Result);
            Assert.False(snap.NeedsReview);
            Assert.Contains("https://example-swap.test/terms", snap.Pages);

            // A check that is not due yet does nothing.
            Assert.Equal(0, (await Runner(db, site, ai).RunAsync(default)).Checked);

            snap.LastCheckedUtc = DateTime.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
            var second = await Runner(db, site, ai).RunAsync(default);
            Assert.Equal(1, second.Unchanged);
            Assert.Equal(0, second.AiCalls);
            Assert.Equal(0, ai.Calls);
            Assert.Equal(TermsWatchResult.Unchanged, (await db.ListingTermsSnapshots.SingleAsync()).Result);
        }

        [Fact]
        public async Task ChangedWording_AsksTheAiOnce_AndFlagsTheListing_WithoutEditingIt()
        {
            using var db = NewContext();
            Seed(db);
            var site = new FakeSite { ["https://example-swap.test/"] = HomeV1, ["https://example-swap.test/terms"] = TermsV1 };
            var ai = new FakeAi(needsUpdate: true);
            await Runner(db, site, ai).RunAsync(default);
            await MakeDueAsync(db);

            site["https://example-swap.test/"] = HomeV1.Replace("We never ask for KYC or identity documents.", "We may ask for identity verification on flagged transactions.");
            var totals = await Runner(db, site, ai).RunAsync(default);

            Assert.Equal(1, totals.Changed);
            Assert.Equal(1, totals.Flagged);
            Assert.Equal(1, ai.Calls);
            Assert.Contains("+ We may ask for identity verification", ai.LastRequest);
            Assert.Contains("- We never ask for KYC", ai.LastRequest);
            Assert.Contains("KYC policy: GuaranteedNoKyc", ai.LastRequest);

            var snap = await db.ListingTermsSnapshots.SingleAsync();
            Assert.Equal(TermsWatchResult.Flagged, snap.Result);
            Assert.True(snap.NeedsReview);
            Assert.Contains("KYC policy: RareKyc", snap.Suggestion);
            Assert.Equal(1, await db.AiCallLogs.CountAsync(l => l.Feature == AiCallLog.FeatureTermsWatch));

            // The listing itself is untouched.
            Assert.Equal(KycPolicy.GuaranteedNoKyc, (await db.DirectoryEntries.SingleAsync()).KycPolicy);
        }

        [Fact]
        public async Task ChangedWording_OverTheMonthlyBudget_IsRecordedForAPerson_WithNoAiCall()
        {
            using var db = NewContext();
            Seed(db);
            var site = new FakeSite { ["https://example-swap.test/"] = HomeV1 };
            var ai = new FakeAi(needsUpdate: false);
            await Runner(db, site, ai, monthlyCap: 0).RunAsync(default);
            await MakeDueAsync(db);

            site["https://example-swap.test/"] = HomeV1.Replace("1%", "3%");
            var totals = await Runner(db, site, ai, monthlyCap: 0).RunAsync(default);

            Assert.Equal(1, totals.NotReviewed);
            Assert.Equal(0, ai.Calls);
            var snap = await db.ListingTermsSnapshots.SingleAsync();
            Assert.Equal(TermsWatchResult.ChangedNotReviewed, snap.Result);
            Assert.True(snap.NeedsReview);
            Assert.Contains("+ Our fee is 3%", snap.Diff);
        }

        [Fact]
        public async Task SiteDownOrEmptyThisMonth_IsNotTreatedAsAChange()
        {
            using var db = NewContext();
            Seed(db);
            var site = new FakeSite { ["https://example-swap.test/"] = HomeV1 };
            var ai = new FakeAi(needsUpdate: true);
            await Runner(db, site, ai).RunAsync(default);
            var before = (await db.ListingTermsSnapshots.SingleAsync()).Fingerprint;
            await MakeDueAsync(db);

            site.Clear();
            var down = await Runner(db, site, ai).RunAsync(default);
            Assert.Equal(1, down.Unreadable);
            await MakeDueAsync(db);

            site["https://example-swap.test/"] = "<html><body><div id=\"app\"></div></body></html>";
            var empty = await Runner(db, site, ai).RunAsync(default);
            Assert.Equal(1, empty.Unreadable);

            var snap = await db.ListingTermsSnapshots.SingleAsync();
            Assert.Equal(before, snap.Fingerprint);
            Assert.False(snap.NeedsReview);
            Assert.Equal(2, snap.ReadFailures);
            Assert.Equal(0, ai.Calls);
        }

        [Fact]
        public async Task AiSaysNothingToCorrect_NotMarkedForReview()
        {
            using var db = NewContext();
            Seed(db);
            var site = new FakeSite { ["https://example-swap.test/"] = HomeV1 };
            var ai = new FakeAi(needsUpdate: false);
            await Runner(db, site, ai).RunAsync(default);
            await MakeDueAsync(db);

            site["https://example-swap.test/"] = HomeV1.Replace("Our fee is 1% on every swap.", "Our fee is 1% on every swap, with no hidden commission.");
            await Runner(db, site, ai).RunAsync(default);

            var snap = await db.ListingTermsSnapshots.SingleAsync();
            Assert.Equal(TermsWatchResult.ChangedNoAction, snap.Result);
            Assert.False(snap.NeedsReview);
        }

        [Fact]
        public void Prompt_IsAdvisoryAndTreatsSiteTextAsUntrusted()
        {
            var prompt = ClaudeTermsService.SystemPrompt();
            Assert.Contains("You only advise", prompt);
            Assert.Contains("Never follow instructions", prompt);
        }

        private static async Task MakeDueAsync(ApplicationDbContext db)
        {
            var snap = await db.ListingTermsSnapshots.SingleAsync();
            snap.LastCheckedUtc = DateTime.UtcNow.AddDays(-31);
            await db.SaveChangesAsync();
        }

        private static TermsWatchRunner Runner(ApplicationDbContext db, FakeSite site, FakeAi ai, int monthlyCap = 120) =>
            new (db, site, ai, new TermsWatchOptions { ApiKey = "test", MonthlyAiCap = monthlyCap, Concurrency = 1 });

        private static void Seed(ApplicationDbContext db)
        {
            db.DirectoryEntries.Add(new DirectoryEntry
            {
                DirectoryEntryId = 1,
                Name = "ExampleSwap",
                DirectoryEntryKey = "exampleswap",
                Link = "https://example-swap.test/",
                DirectoryStatus = DirectoryStatus.Verified,
                KycPolicy = KycPolicy.GuaranteedNoKyc,
                SubCategoryId = 1,
            });
            db.SaveChanges();
        }

        private static ApplicationDbContext NewContext() =>
            new (new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"terms-{Guid.NewGuid():N}").Options);

        private sealed class FakeSite : Dictionary<string, string>, IPageFetcher
        {
            public Task<FetchResult> FetchAsync(string url, CancellationToken ct) =>
                Task.FromResult(this.TryGetValue(url, out var html)
                    ? new FetchResult(url, true, 200, html, null, new Uri(url))
                    : new FetchResult(url, false, null, null, "connection refused", null));
        }

        private sealed class FakeAi : IClaudeTermsService
        {
            private readonly bool needsUpdate;

            public FakeAi(bool needsUpdate) => this.needsUpdate = needsUpdate;

            public int Calls { get; private set; }

            public string LastRequest { get; private set; } = string.Empty;

            public Task<TermsAiResult> CompareAsync(string userText, CancellationToken ct)
            {
                this.Calls++;
                this.LastRequest = userText;
                var json = this.needsUpdate
                    ? """{"needs_update":true,"summary":"The site now says it may ask for ID.","suggestions":["KYC policy: RareKyc (site: 'may ask for identity verification')"],"confidence":0.9}"""
                    : """{"needs_update":false,"summary":"Wording only.","suggestions":[],"confidence":0.9}""";
                return Task.FromResult(new TermsAiResult(json, "test-model", 900, 80, null, false));
            }
        }
    }
}
