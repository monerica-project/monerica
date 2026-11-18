using System.Net;
using System.Text.Json;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Models.StaticPublishing;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Tests.Services
{
    /// <summary>The enqueue service against a real (in-memory) database, and the internal endpoint.</summary>
    public class StaticPublishServiceTests
    {
        private const string Secret = "0123456789abcdef0123456789abcdef";

        [Fact]
        public async Task ModeOff_QueuesNothing()
        {
            using var db = Seed();
            var svc = Service(db, StaticPublishMode.Off);

            await svc.EnqueueForEntryAsync(1, "listing edited: Alpha");
            await svc.RequestFullCrawlAsync("deploy");

            Assert.Empty(db.StaticPublishQueueItems);
            Assert.Empty(await svc.PathsForEntryAsync(1));
        }

        [Fact]
        public async Task ListingEdit_QueuesEveryPageTheListingIsOn()
        {
            using var db = Seed();
            var svc = Service(db, StaticPublishMode.On);

            await svc.EnqueueForEntryAsync(1, "listing edited: Alpha");

            var paths = db.StaticPublishQueueItems.Select(q => q.Path).ToList();
            foreach (var p in new[] { "/site/alpha", "/exchanges/swaps", "/exchanges", "/wallets/mobile", "/wallets", "/tagged/no-kyc", "/tagged", "/countries/canada", "/countries", "/", "/newest", "/sitemap.xml" })
            {
                Assert.Contains(p, paths);
            }

            Assert.All(db.StaticPublishQueueItems, q => Assert.Equal("listing edited: Alpha", q.Reason));
        }

        [Fact]
        public async Task ListingMovedToAnotherSubcategory_QueuesTheOldPagesToo()
        {
            using var db = Seed();
            var svc = Service(db, StaticPublishMode.On);

            var before = await svc.PathsForEntryAsync(1);
            var entry = await db.DirectoryEntries.SingleAsync(e => e.DirectoryEntryId == 1);
            entry.SubCategoryId = 30; // businesses/shops
            entry.DirectoryEntryKey = "alpha-renamed";
            entry.CountryCode = null;
            await db.SaveChangesAsync();

            await svc.EnqueueForEntryAsync(1, "listing edited: Alpha", before);

            var paths = db.StaticPublishQueueItems.Select(q => q.Path).ToList();
            foreach (var p in new[] { "/site/alpha", "/site/alpha-renamed", "/exchanges/swaps", "/exchanges", "/businesses/shops", "/businesses", "/countries/canada" })
            {
                Assert.Contains(p, paths);
            }
        }

        [Fact]
        public async Task ReviewAndReplyChanges_QueueTheListingsPages()
        {
            using var db = Seed();
            var svc = Service(db, StaticPublishMode.DryRun);

            await svc.EnqueueForReplyAsync(500, "reply approved");

            Assert.Contains(db.StaticPublishQueueItems, q => q.Path == "/site/alpha" && q.Reason == "reply approved");
            Assert.Contains(db.StaticPublishQueueItems, q => q.Path == "/reviews");
        }

        [Fact]
        public async Task FullCrawlRequest_IsOneRow()
        {
            using var db = Seed();
            var svc = Service(db, StaticPublishMode.On);

            await svc.RequestFullCrawlAsync("content snippet edited: FooterHtml");
            await svc.RequestFullCrawlAsync("sponsorship paid");

            var row = Assert.Single(db.StaticPublishQueueItems);
            Assert.Equal(StaticPublishConstants.FullCrawlPath, row.Path);
            Assert.Equal("sponsorship paid", row.Reason);
        }

        [Fact]
        public async Task InternalPending_Is404_WithoutTheSecret_OrOnThePublicPort()
        {
            using var db = Seed();
            var noSecret = Controller(db, StaticPublishMode.On, port: 5056, secret: null);
            Assert.IsType<NotFoundResult>(await noSecret.Pending());

            var publicPort = Controller(db, StaticPublishMode.On, port: 5055, secret: Secret);
            Assert.IsType<NotFoundResult>(await publicPort.Pending());

            var proxied = Controller(db, StaticPublishMode.On, port: 5056, secret: Secret, remote: IPAddress.Parse("198.51.100.7"));
            Assert.IsType<NotFoundResult>(await proxied.Pending());
        }

        [Fact]
        public async Task InternalPending_HandsOutPaths_AndSaysDryRun_InDryRunMode()
        {
            using var db = Seed();
            await Service(db, StaticPublishMode.DryRun).EnqueueAsync(new[] { "/site/alpha", "/" }, "manual");
            var c = Controller(db, StaticPublishMode.DryRun, port: 5056, secret: Secret);

            var result = Assert.IsType<JsonResult>(await c.Pending());
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
            Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("fullCrawlRequested").GetBoolean());
            Assert.Equal(2, doc.RootElement.GetProperty("paths").GetArrayLength());
        }

        [Fact]
        public async Task InternalPending_StartsACrawlOnlyForForcedRequests_NotForSiteWideMarkers()
        {
            using var db = Seed();
            var svc = Service(db, StaticPublishMode.On);
            var c = Controller(db, StaticPublishMode.On, port: 5056, secret: Secret);

            await svc.RequestFullCrawlAsync("content snippet edited: FooterHtml");
            Assert.False(Flag(await c.Pending()));

            await svc.RequestFullCrawlAsync("review tag edited", runNow: true);
            Assert.True(Flag(await c.Pending()));
        }

        private static bool Flag(IActionResult r)
        {
            var json = Assert.IsType<JsonResult>(r);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(json.Value));
            return doc.RootElement.GetProperty("fullCrawlRequested").GetBoolean();
        }

        private static StaticPublishService Service(ApplicationDbContext db, StaticPublishMode mode) =>
            new StaticPublishService(
                db,
                new StaticPublishRepository(db),
                new DirectoryEntryRepository(db, new DirectoryEntriesAuditRepository(db)),
                new TagRepository(db),
                new FixedOptions(new StaticPublishOptions { Mode = mode }),
                NullLogger<StaticPublishService>.Instance);

        private static StaticPublishInternalController Controller(ApplicationDbContext db, StaticPublishMode mode, int port, string? secret, IPAddress? remote = null)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.LocalPort = port;
            ctx.Connection.RemoteIpAddress = remote ?? IPAddress.Loopback;
            if (secret != null)
            {
                ctx.Request.Headers["X-Static-Publish-Key"] = secret;
            }

            var options = new FixedOptions(new StaticPublishOptions { Mode = mode, InternalPort = 5056, InternalSecret = Secret });
            return new StaticPublishInternalController(new StaticPublishRepository(db), options, NullLogger<StaticPublishInternalController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = ctx },
            };
        }

        private static ApplicationDbContext Seed()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"static-publish-svc-{Guid.NewGuid():N}").Options);

            db.Categories.AddRange(
                new Category { CategoryId = 1, Name = "Exchanges", CategoryKey = "exchanges", IsEnabled = true },
                new Category { CategoryId = 2, Name = "Wallets", CategoryKey = "wallets", IsEnabled = true },
                new Category { CategoryId = 3, Name = "Businesses", CategoryKey = "businesses", IsEnabled = true });
            db.Subcategories.AddRange(
                new Subcategory { SubCategoryId = 10, CategoryId = 1, Name = "Swaps", SubCategoryKey = "swaps", IsEnabled = true },
                new Subcategory { SubCategoryId = 20, CategoryId = 2, Name = "Mobile", SubCategoryKey = "mobile", IsEnabled = true },
                new Subcategory { SubCategoryId = 30, CategoryId = 3, Name = "Shops", SubCategoryKey = "shops", IsEnabled = true });
            db.DirectoryEntries.Add(new DirectoryEntry
            {
                DirectoryEntryId = 1,
                Name = "Alpha",
                DirectoryEntryKey = "alpha",
                Link = "https://alpha.example",
                SubCategoryId = 10,
                DirectoryStatus = DirectoryStatus.Admitted,
                CountryCode = "CA",
                CreateDate = DateTime.UtcNow.AddDays(-200),
            });
            db.DirectoryEntryAdditionalSubcategories.Add(new DirectoryEntryAdditionalSubcategory { DirectoryEntryId = 1, SubCategoryId = 20 });
            db.Tags.Add(new Tag { TagId = 7, Name = "No KYC", Key = "no-kyc" });
            db.DirectoryEntryTags.Add(new DirectoryEntryTag { DirectoryEntryId = 1, TagId = 7 });
            db.DirectoryEntryReviews.Add(new DirectoryManager.Data.Models.Reviews.DirectoryEntryReview
            {
                DirectoryEntryReviewId = 100,
                DirectoryEntryId = 1,
                Body = "Good",
                ModerationStatus = ReviewModerationStatus.Approved,
                AuthorFingerprint = "ABC",
            });
            db.DirectoryEntryReviewComments.Add(new DirectoryManager.Data.Models.Reviews.DirectoryEntryReviewComment
            {
                DirectoryEntryReviewCommentId = 500,
                DirectoryEntryReviewId = 100,
                Body = "Thanks",
                AuthorFingerprint = "DEF",
            });
            db.SaveChanges();
            return db;
        }

        private sealed class FixedOptions : IOptionsMonitor<StaticPublishOptions>
        {
            public FixedOptions(StaticPublishOptions value) => this.CurrentValue = value;

            public StaticPublishOptions CurrentValue { get; }

            public StaticPublishOptions Get(string? name) => this.CurrentValue;

            public IDisposable? OnChange(Action<StaticPublishOptions, string?> listener) => null;
        }
    }
}
