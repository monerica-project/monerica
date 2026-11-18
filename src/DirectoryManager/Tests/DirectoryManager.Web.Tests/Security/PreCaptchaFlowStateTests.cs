using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DirectoryManager.Web.Tests.Security
{
    // The public no-JS flows (review, reply, my-reviews, problem report, verification request)
    // must not allocate server state before the captcha is solved: "begin" only mints a signed
    // ticket, and the flow is stored on a successful captcha POST. These tests drive the real
    // controllers over an in-memory database and a real MemoryCache.
    public class PreCaptchaFlowStateTests
    {
        private const int EntryId = 10;
        private const int FloodSize = 5000;

        [Fact]
        public async Task ProblemReport_FlowStillCompletes_AndNothingIsStoredBeforeCaptcha()
        {
            using var h = new Harness();
            var c = h.ProblemReports();

            var begin = Assert.IsType<RedirectToActionResult>(await c.Begin(EntryId, website: null));
            Assert.Equal(nameof(ProblemReportsController.Captcha), begin.ActionName);
            var (flowId, ticket) = FlowOf(begin);
            Assert.Equal(0, h.Cache.Count);

            // Captcha page renders and carries the ticket into the form.
            var page = Assert.IsType<ViewResult>(c.Captcha(flowId, ticket));
            Assert.Equal(ticket, (string)c.ViewBag.FlowTicket);

            // Wrong captcha: page re-displays (ticket kept), still nothing stored.
            h.Captcha.Accept = false;
            h.NewRequest(c);
            var retry = Assert.IsType<ViewResult>(c.CaptchaPost(flowId, ticket));
            Assert.Equal("Captcha", retry.ViewName);
            Assert.Equal(ticket, (string)c.ViewBag.FlowTicket);
            Assert.Equal(0, h.Cache.Count);

            // Right captcha: now (and only now) the flow exists.
            h.Captcha.Accept = true;
            h.NewRequest(c);
            var toCompose = Assert.IsType<RedirectToActionResult>(c.CaptchaPost(flowId, ticket));
            Assert.Equal(nameof(ProblemReportsController.Compose), toCompose.ActionName);
            Assert.Equal(1, h.Cache.Count);

            h.NewRequest(c);
            Assert.IsType<ViewResult>(await c.Compose(flowId));

            // Back button to the captcha page still works (flow exists; ticket optional).
            h.NewRequest(c);
            Assert.IsType<ViewResult>(c.Captcha(flowId, ticket: null));
        }

        [Fact]
        public async Task VerificationRequest_FlowStillCompletes()
        {
            using var h = new Harness();
            var c = h.VerificationRequests();

            var (flowId, ticket) = FlowOf(Assert.IsType<RedirectToActionResult>(await c.Begin(EntryId, website: null)));
            Assert.Equal(0, h.Cache.Count);

            h.NewRequest(c);
            Assert.IsType<ViewResult>(c.Captcha(flowId, ticket));
            h.NewRequest(c);
            var next = Assert.IsType<RedirectToActionResult>(c.CaptchaPost(flowId, ticket));
            Assert.Equal(nameof(VerificationRequestsController.Compose), next.ActionName);

            h.NewRequest(c);
            Assert.IsType<ViewResult>(await c.Compose(flowId));
        }

        [Fact]
        public async Task Review_FlowReachesKeyStep_AndRejectsMissingOrForeignTickets()
        {
            using var h = new Harness();
            var c = h.Reviews();

            var (flowId, ticket) = FlowOf(Assert.IsType<RedirectToActionResult>(await c.Begin(EntryId, website: null)));
            Assert.Equal(0, h.Cache.Count);

            // No ticket / ticket for another flow id / ticket of another flow kind: refused.
            Assert.IsType<BadRequestObjectResult>(c.Captcha(flowId, ticket: null));
            Assert.IsType<BadRequestObjectResult>(c.Captcha(Guid.NewGuid(), ticket));
            var foreign = PreCaptchaFlowTicket.Issue(h.DataProtection, "problem-report", flowId, EntryId);
            Assert.IsType<BadRequestObjectResult>(c.CaptchaPost(flowId, foreign));
            Assert.IsType<BadRequestObjectResult>(c.CaptchaPost(flowId, ticket + "x"));
            Assert.Equal(0, h.Cache.Count);

            h.NewRequest(c);
            var next = Assert.IsType<RedirectToActionResult>(c.CaptchaPost(flowId, ticket));
            Assert.Equal(nameof(DirectoryEntryReviewsController.SubmitKey), next.ActionName);
            h.NewRequest(c);
            Assert.IsType<ViewResult>(c.SubmitKey(flowId));
        }

        [Fact]
        public void Reply_And_MyReviews_FlowsReachKeyStep()
        {
            using var h = new Harness();

            var replies = h.Replies();
            var replyFlow = Guid.NewGuid();
            var replyTicket = PreCaptchaFlowTicket.Issue(h.DataProtection, "review-reply", replyFlow, 5, EntryId);
            Assert.IsType<ViewResult>(replies.Captcha(replyFlow, replyTicket));
            Assert.Equal(5, (int)replies.ViewBag.DirectoryEntryReviewId);
            h.NewRequest(replies);
            Assert.IsType<RedirectToActionResult>(replies.CaptchaPost(replyFlow, replyTicket));
            h.NewRequest(replies);
            Assert.IsType<ViewResult>(replies.SubmitKey(replyFlow));

            var mine = h.MyReviews();
            var (myFlow, myTicket) = FlowOf(Assert.IsType<RedirectToActionResult>(mine.Begin(website: null)));
            Assert.Equal(1, h.Cache.Count); // only the reply flow so far
            h.NewRequest(mine);
            Assert.IsType<ViewResult>(mine.Captcha(myFlow, myTicket));
            h.NewRequest(mine);
            Assert.IsType<RedirectToActionResult>(mine.CaptchaPost(myFlow, myTicket));
            h.NewRequest(mine);
            Assert.IsType<ViewResult>(mine.SubmitKey(myFlow));
            Assert.Equal(2, h.Cache.Count);
        }

        [Fact]
        public async Task FloodOfBegins_StoresNothing_AndVerifiedFlowsSurvive()
        {
            using var h = new Harness();
            var reviews = h.Reviews();
            var reports = h.ProblemReports();
            var mine = h.MyReviews();

            // A real visitor gets past the captcha first...
            var (realFlow, realTicket) = FlowOf(Assert.IsType<RedirectToActionResult>(await reviews.Begin(EntryId, website: null)));
            h.NewRequest(reviews);
            Assert.IsType<RedirectToActionResult>(reviews.CaptchaPost(realFlow, realTicket));
            Assert.Equal(1, h.Cache.Count);

            // ...then an anonymous flood hits every begin endpoint.
            for (var i = 0; i < FloodSize; i++)
            {
                await reviews.Begin(EntryId, website: null);
                await reports.Begin(EntryId, website: null);
                mine.Begin(website: null);
            }

            // Not one entry was allocated by the flood (cap is effectively zero pre-captcha).
            Assert.Equal(1, h.Cache.Count);

            // The real visitor carries on.
            h.NewRequest(reviews);
            Assert.IsType<ViewResult>(reviews.SubmitKey(realFlow));
        }

        [Fact]
        public void PendingFlowLimiter_CapsPerScope_EvictingOldestFirst()
        {
            var limiter = new PendingFlowLimiter(perScopeCap: 3, globalCap: 100);
            var now = DateTime.UtcNow;
            var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

            var evictedAll = new List<Guid>();
            foreach (var id in ids)
            {
                Assert.True(limiter.TryAdd(1, id, now.AddMinutes(20), now, out var evicted));
                evictedAll.AddRange(evicted);
            }

            Assert.Equal(new[] { ids[0], ids[1] }, evictedAll);
            Assert.Equal(3, limiter.Count);

            // Another scope is unaffected.
            Assert.True(limiter.TryAdd(2, Guid.NewGuid(), now.AddMinutes(20), now, out var none));
            Assert.Empty(none);
            Assert.Equal(4, limiter.Count);

            limiter.Remove(1, ids[4]);
            Assert.Equal(3, limiter.Count);
        }

        [Fact]
        public void PendingFlowLimiter_GlobalCapRefuses_UntilEntriesExpire()
        {
            var limiter = new PendingFlowLimiter(perScopeCap: 2, globalCap: 10);
            var now = DateTime.UtcNow;

            for (var scope = 0; scope < FloodSize; scope++)
            {
                limiter.TryAdd(scope, Guid.NewGuid(), now.AddMinutes(20), now, out _);
            }

            Assert.Equal(10, limiter.Count);
            Assert.False(limiter.TryAdd(-1, Guid.NewGuid(), now.AddMinutes(20), now, out _));

            // Once the flood's entries expire there is room again.
            Assert.True(limiter.TryAdd(-1, Guid.NewGuid(), now.AddMinutes(41), now.AddMinutes(21), out _));
            Assert.Equal(1, limiter.Count);
        }

        [Fact]
        public void CaptchaContexts_AreNormalized_AndBoundedPerSession()
        {
            Assert.Equal("review", CaptchaTools.NormalizeContext("review"));
            Assert.Equal("newsletter-unsubscribe", CaptchaTools.NormalizeContext(" newsletter-unsubscribe "));
            Assert.Equal("default", CaptchaTools.NormalizeContext(null));
            Assert.Equal("default", CaptchaTools.NormalizeContext("<script>"));
            Assert.Equal("default", CaptchaTools.NormalizeContext(new string('a', CaptchaTools.MaxContextLength + 1)));

            var session = new TestSession();
            for (var i = 0; i < FloodSize; i++)
            {
                CaptchaTools.StoreBounded(session, "ctx" + i, "ABCDE");
            }

            Assert.True(session.Keys.Count() <= CaptchaTools.MaxContextsPerSession + 2);

            // The most recent captcha still validates.
            CaptchaTools.StoreBounded(session, "review", "XYZ12");
            var http = new DefaultHttpContext { Session = session };
            Assert.True(CaptchaTools.Validate(http, "review", "xyz12"));
        }

        private static (Guid FlowId, string Ticket) FlowOf(RedirectToActionResult r)
        {
            var flowId = Assert.IsType<Guid>(r.RouteValues!["flowId"]);
            var ticket = Assert.IsType<string>(r.RouteValues![PreCaptchaFlowTicket.FieldName]);
            return (flowId, ticket);
        }

        private sealed class Harness : IDisposable
        {
            private readonly ApplicationDbContext db;
            private readonly IDirectoryEntryRepository entries;

            public Harness()
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"precaptcha-{Guid.NewGuid()}")
                    .Options;
                this.db = new ApplicationDbContext(options);

                var category = new Category { CategoryId = 1, Name = "Exchanges", CategoryKey = "exchanges" };
                var sub = new Subcategory { SubCategoryId = 2, Name = "Instant Swaps", SubCategoryKey = "instant-swaps", Category = category, CategoryId = 1 };
                this.db.Categories.Add(category);
                this.db.Subcategories.Add(sub);
                this.db.DirectoryEntries.Add(new DirectoryEntry
                {
                    DirectoryEntryId = EntryId,
                    Name = "Example Swap",
                    DirectoryEntryKey = "example-swap",
                    Link = "https://example.com",
                    SubCategory = sub,
                    SubCategoryId = 2,
                    DirectoryStatus = DirectoryStatus.Verified,
                });
                this.db.SaveChanges();

                this.entries = new DirectoryEntryRepository(this.db, new DirectoryEntriesAuditRepository(this.db));
            }

            public MemoryCache Cache { get; } = new (new MemoryCacheOptions());

            public FakeCaptcha Captcha { get; } = new ();

            public IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();

            public ProblemReportsController ProblemReports() =>
                this.Init(new ProblemReportsController(this.Cache, this.Captcha, this.entries, null!, null!, null!, this.DataProtection));

            public VerificationRequestsController VerificationRequests() =>
                this.Init(new VerificationRequestsController(this.Cache, this.Captcha, this.entries, null!, null!, null!, this.DataProtection));

            public DirectoryEntryReviewsController Reviews() =>
                this.Init(new DirectoryEntryReviewsController(null!, this.Cache, this.Captcha, null!, this.entries, null!, null!, null!, null!, null!, this.DataProtection));

            public DirectoryEntryReviewRepliesController Replies() =>
                this.Init(new DirectoryEntryReviewRepliesController(this.Cache, this.Captcha, null!, null!, null!, this.entries, null!, null!, null!, this.DataProtection));

            public MyReviewsController MyReviews() =>
                this.Init(new MyReviewsController(this.Cache, this.Captcha, null!, null!, null!, this.DataProtection));

            public void NewRequest(Controller c)
            {
                var http = new DefaultHttpContext { Session = new TestSession() };
                http.Request.ContentType = "application/x-www-form-urlencoded";
                http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
                c.ControllerContext = new ControllerContext { HttpContext = http };
            }

            public void Dispose()
            {
                this.Cache.Dispose();
                this.db.Dispose();
            }

            private T Init<T>(T c)
                where T : Controller
            {
                this.NewRequest(c);
                return c;
            }
        }

        private sealed class FakeCaptcha : ICaptchaService
        {
            public bool Accept { get; set; } = true;

            public bool IsValid(HttpRequest request) => this.Accept;
        }

        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> store = new ();

            public bool IsAvailable => true;

            public string Id { get; } = Guid.NewGuid().ToString();

            public IEnumerable<string> Keys => this.store.Keys.ToList();

            public void Clear() => this.store.Clear();

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Remove(string key) => this.store.Remove(key);

            public void Set(string key, byte[] value) => this.store[key] = value;

            public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) =>
                this.store.TryGetValue(key, out value);
        }
    }
}
