using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.GuaranteeSync;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DirectoryManager.GuaranteeSync.Tests
{
    public class GuaranteeSyncTests
    {
        private const string Feed = """
{"status":"ok","count":6,"services":[
 {"service_name":"pablocash","link_clearnet":"https://orangefren.com/goto/pablocash/","guarantee_btc":0.75,"guarantee_usd":1},
 {"service_name":"cce","guarantee_btc":0.308},
 {"service_name":"ghostswap","guarantee_btc":0.40955328},
 {"service_name":"hiddenswap","guarantee_btc":0.02},
 {"service_name":"newcomer","guarantee_btc":0.05},
 {"service_name":"broken","guarantee_btc":"x"},
 {"service_name":"removedswap","guarantee_btc":0.5}]}
""";

        [Fact]
        public async Task UpdatesChangedAmounts_AddsMissingRows_KeepsLinksAndOtherGuarantees_NeverDeletes()
        {
            using var db = NewContext();
            Entry(db, 1, "Pablo.Cash", "pablo-cash");
            Entry(db, 2, "CCE Cash", "cce-cash");
            Entry(db, 3, "GhostSwap", "ghostswap");
            Entry(db, 4, "Newcomer", "newcomer");
            Entry(db, 5, "Bitania", "bitania");
            Entry(db, 6, "RemovedSwap", "removedswap", DirectoryStatus.Removed);
            Row(db, 1, 1, "https://orangefren.com/guarantee", 0.69046027m, Currency.BTC);
            Row(db, 1, 2, "https://bitcointalk.org/x", 10000m, Currency.USD);
            Row(db, 2, 1, "https://orangefren.com/guarantee", 0.308m, Currency.BTC);
            Row(db, 3, 1, "https://kycnone.com/x", 5000m, Currency.USD);
            Row(db, 5, 1, "https://orangefren.com/guarantee", 0.59877887m, Currency.BTC);
            await db.SaveChangesAsync();

            var feed = OrangeFrenGuarantees.Parse(Feed, out var problem)!;
            Assert.Null(problem);
            Assert.Equal(6, feed.Count);

            var options = new GuaranteeSyncOptions();
            options.Aliases["cce"] = "cce-cash";
            var result = await OrangeFrenGuarantees.ApplyAsync(db, feed, options, default);

            Assert.Null(result.Problem);
            Assert.Equal(new[] { "GhostSwap:added", "Newcomer:added", "Pablo.Cash:updated" }, result.Changes.Select(c => $"{c.Listing}:{c.Kind}").OrderBy(x => x));
            Assert.Equal(new[] { "hiddenswap", "removedswap" }, result.Unmatched.OrderBy(x => x));
            Assert.Equal(new[] { "Bitania" }, result.NotInFeed);

            var rows = await db.DirectoryEntryGuarantees.AsNoTracking().OrderBy(g => g.DirectoryEntryId).ThenBy(g => g.SortOrder).ToListAsync();
            Assert.Equal(7, rows.Count);
            var pablo = rows.Single(g => g.DirectoryEntryId == 1 && g.SortOrder == 1);
            Assert.Equal(0.75m, pablo.Amount);
            Assert.Equal("https://orangefren.com/guarantee", pablo.Link);
            Assert.Equal(10000m, rows.Single(g => g.DirectoryEntryId == 1 && g.SortOrder == 2).Amount);
            var ghost = rows.Single(g => g.DirectoryEntryId == 3 && g.SortOrder == 2);
            Assert.Equal("https://orangefren.com/guarantee", ghost.Link);
            Assert.Equal(Currency.BTC, ghost.Currency);
            Assert.Equal(0.40955328m, ghost.Amount);
            Assert.Equal(0.59877887m, rows.Single(g => g.DirectoryEntryId == 5).Amount);
            Assert.DoesNotContain(rows, g => g.DirectoryEntryId == 6);

            // The changed listing pages are queued for the static mirror.
            var queued = await db.StaticPublishQueueItems.Select(q => q.Path).OrderBy(p => p).ToListAsync();
            Assert.Equal(new[] { "/site/ghostswap", "/site/newcomer", "/site/pablo-cash" }, queued);

            // Running again changes nothing.
            var again = await OrangeFrenGuarantees.ApplyAsync(db, feed, options, default);
            Assert.Empty(again.Changes);
        }

        [Theory]
        [InlineData("{\"status\":\"error\",\"services\":[]}")]
        [InlineData("not json")]
        [InlineData("{\"status\":\"ok\"}")]
        public void BrokenFeed_IsRejected(string json)
        {
            Assert.Null(OrangeFrenGuarantees.Parse(json, out var problem));
            Assert.NotNull(problem);
        }

        [Fact]
        public async Task TooFewServices_ChangesNothing()
        {
            using var db = NewContext();
            Entry(db, 1, "Pablo.Cash", "pablo-cash");
            Row(db, 1, 1, "https://orangefren.com/guarantee", 0.69m, Currency.BTC);
            await db.SaveChangesAsync();

            var result = await OrangeFrenGuarantees.ApplyAsync(db, new[] { new FeedService("pablocash", 0.01m) }, new GuaranteeSyncOptions(), default);
            Assert.NotNull(result.Problem);
            Assert.Equal(0.69m, (await db.DirectoryEntryGuarantees.SingleAsync()).Amount);
        }

        [Theory]
        [InlineData("https://orangefren.com/guarantee", true)]
        [InlineData("https://www.orangefren.com/goto/cce/", true)]
        [InlineData("https://orangefren.com.evil.example/guarantee", false)]
        [InlineData("https://cyphergoat.com/transparency", false)]
        public void OrangeFrenLink_IsRecognizedByHost(string link, bool expected)
        {
            Assert.Equal(expected, OrangeFrenGuarantees.IsOrangeFrenLink(link));
        }

        private static void Entry(ApplicationDbContext db, int id, string name, string key, DirectoryStatus status = DirectoryStatus.Verified) =>
            db.DirectoryEntries.Add(new DirectoryEntry { DirectoryEntryId = id, Name = name, DirectoryEntryKey = key, Link = $"https://{key}.example", DirectoryStatus = status, SubCategoryId = 1 });

        private static void Row(ApplicationDbContext db, int entryId, int sort, string link, decimal amount, Currency currency) =>
            db.DirectoryEntryGuarantees.Add(new DirectoryEntryGuarantee { DirectoryEntryId = entryId, SortOrder = sort, Link = link, Amount = amount, Currency = currency, CreatedByUserId = "test" });

        private static ApplicationDbContext NewContext() =>
            new (new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"guar-{Guid.NewGuid():N}").Options);
    }
}
