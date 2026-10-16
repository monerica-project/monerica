using System.Xml.Linq;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DirectoryManager.Web.Tests.Helpers
{
    // Regression tests for the 2026-10 bug-fix pass (web layer).
    public class BugFixRegressionTests
    {
        // ---------- cache tag helper: expiry never <= 0 ----------
        [Fact]
        public void CacheExpiration_NextExpiryInThePast_ClampsToOneSecond()
        {
            var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal(TimeSpan.FromSeconds(1), CacheContentTagHelper.ComputeExpiration(now.AddMinutes(-5), now, 600));
            Assert.Equal(TimeSpan.FromSeconds(1), CacheContentTagHelper.ComputeExpiration(now, now, 600));
            Assert.Equal(TimeSpan.FromSeconds(30), CacheContentTagHelper.ComputeExpiration(now.AddSeconds(30), now, 600));
            Assert.Equal(TimeSpan.FromSeconds(600), CacheContentTagHelper.ComputeExpiration(null, now, 600));
            Assert.Equal(TimeSpan.FromSeconds(600), CacheContentTagHelper.ComputeExpiration(DateTime.MinValue, now, 600));
            Assert.Equal(TimeSpan.FromSeconds(1), CacheContentTagHelper.ComputeExpiration(null, now, 0));
        }

        // ---------- filter cache invalidation uses the prefixes actually registered ----------
        [Fact]
        public void ClearSponsorCaches_ExpiresEveryFilterCachePrefix()
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());

            foreach (var prefix in StringConstants.FilterCacheTokenPrefixes)
            {
                cache.Set("key-" + prefix, "v", new MemoryCacheEntryOptions().AddExpirationToken(CachePrefixManager.GetToken(prefix)));
            }

            Assert.Contains(StringConstants.PrefixActiveCategories, StringConstants.FilterCacheTokenPrefixes);
            Assert.Contains(StringConstants.PrefixActiveSubcats, StringConstants.FilterCacheTokenPrefixes);
            Assert.Contains(StringConstants.PrefixActiveTagIdsByCat, StringConstants.FilterCacheTokenPrefixes);

            SponsorCacheHelper.ClearSponsorCaches(cache);

            foreach (var prefix in StringConstants.FilterCacheTokenPrefixes)
            {
                Assert.False(cache.TryGetValue("key-" + prefix, out _), $"entry under '{prefix}' was not expired");
            }
        }

        // ---------- AppendFormat with interpolated strings (braces in user text) ----------
        [Fact]
        public void DirectoryEntryHtml_ScamNameWithBraces_DoesNotThrow()
        {
            var model = new DirectoryEntryViewModel
            {
                DirectoryEntryId = 1,
                Name = "Bad {0} Site }{",
                DirectoryEntryKey = "bad-site",
                Link = "https://bad.example",
                DirectoryStatus = DirectoryStatus.Scam,
            };

            var html = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(model, "https://dir.example");

            Assert.Contains("Bad {0} Site }{", html);
        }

        [Fact]
        public void SearchResultHtml_CategoryNamesWithBraces_DoesNotThrow()
        {
            var model = new DirectoryEntryViewModel
            {
                DirectoryEntryId = 1,
                Name = "Shop",
                DirectoryEntryKey = "shop",
                Link = "https://shop.example",
                DirectoryStatus = DirectoryStatus.Admitted,
                Description = "desc",
                SubCategory = new Subcategory
                {
                    Name = "Sub {1}",
                    SubCategoryKey = "sub",
                    Category = new Category { Name = "Cat {0}", CategoryKey = "cat" },
                },
            };

            var html = DisplayMarkUpHelper.GenerateSearchResultHtml(model, "https://dir.example");

            Assert.Contains("Cat {0}", html);
            Assert.Contains("Sub {1}", html);
        }

        // ---------- paging redirects + canonicals ----------
        [Fact]
        public void FirstPageUrl_DropsOnlyThePageParameter()
        {
            var query = new QueryCollection(new Dictionary<string, StringValues>
            {
                ["q"] = "monero wallet",
                ["Page"] = "99",
                ["TagIds"] = new StringValues(new[] { "1", "2" }),
            });

            var url = PagingRedirectHelper.FirstPageUrl("/filter", query);

            Assert.StartsWith("/filter?", url);
            Assert.DoesNotContain("Page=", url, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("q=monero%20wallet", url);
            Assert.Contains("TagIds=1", url);
            Assert.Contains("TagIds=2", url);
            Assert.Equal("/search", PagingRedirectHelper.FirstPageUrl("/search", new QueryCollection()));
        }

        [Theory]
        [InlineData(1, "/cat/sub")]
        [InlineData(3, "/cat/sub/page/3")]
        public void SubcategoryCanonical_UsesOnlyKnownRouteValues(int page, string expected)
        {
            Assert.Equal(expected, SubCategoryController.BuildSubcategoryCanonicalPath("cat", "sub", page));
        }

        // ---------- invoice report math ----------
        [Fact]
        public void AverageUsdPerXmr_IgnoresInvoicesNotPaidInXmr()
        {
            var invoices = new[]
            {
                Invoice(amountUsd: 300m, paidIn: Currency.XMR, paid: 2m),   // 150 USD/XMR
                Invoice(amountUsd: 150m, paidIn: Currency.XMR, paid: 1m),   // 150 USD/XMR
                Invoice(amountUsd: 5000m, paidIn: Currency.BTC, paid: 0.05m), // must not skew the XMR rate
            };

            Assert.Equal(150m, SponsoredListingInvoiceController.AverageUsdPerUnit(invoices, Currency.XMR));
            Assert.Null(SponsoredListingInvoiceController.AverageUsdPerUnit(new[] { invoices[2] }, Currency.XMR));
        }

        [Fact]
        public void ProratedFutureAmount_TreatsCampaignEndAsExclusive()
        {
            var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddDays(30); // a 30-day offer

            // Not started yet: the whole amount is future revenue (30 days, not 31).
            Assert.Equal(300m, SponsoredListingInvoiceController.ProratedFutureAmount(300m, start, end, start.AddDays(-5)));

            // 10 days in: 20 of 30 days remain.
            Assert.Equal(200m, SponsoredListingInvoiceController.ProratedFutureAmount(300m, start, end, start.AddDays(10)));

            // Ended: nothing left.
            Assert.Equal(0m, SponsoredListingInvoiceController.ProratedFutureAmount(300m, start, end, end));
        }

        // ---------- RSS ----------
        [Fact]
        public void RssFeed_TitlesAreNotDoubleEncoded_AndScamItemsLinkToProfile()
        {
            var scam = new DirectoryEntryWrapper
            {
                DirectoryEntry = new DirectoryEntry
                {
                    DirectoryEntryId = 1,
                    Name = "Rugpull",
                    DirectoryEntryKey = "rugpull",
                    Link = "https://scam.example",
                    DirectoryStatus = DirectoryStatus.Scam,
                },
            };
            var verified = new DirectoryEntryWrapper
            {
                DirectoryEntry = new DirectoryEntry
                {
                    DirectoryEntryId = 2,
                    Name = "Good",
                    DirectoryEntryKey = "good",
                    Link = "https://good.example",
                    DirectoryStatus = DirectoryStatus.Verified,
                },
            };

            var doc = new RssFeedService().GenerateRssFeed(
                new[] { scam, verified }, "Feed", "https://dir.example/rss/feed.xml", "desc");
            var xml = doc.ToString();

            Assert.DoesNotContain("&amp;#x", xml);
            var items = doc.Descendants("item").ToList();
            Assert.Equal("❌ Rugpull", items[0].Element("title")!.Value);
            Assert.Equal("https://dir.example/site/rugpull", items[0].Element("link")!.Value);
            Assert.Equal("✅ Good", items[1].Element("title")!.Value);
            Assert.Equal("https://good.example", items[1].Element("link")!.Value);
        }

        // ---------- owner admin ----------
        [Fact]
        public void ParentComment_MustBelongToTheSameReviewAndNotBeRejected()
        {
            var ok = new DirectoryEntryReviewComment { DirectoryEntryReviewId = 5, ModerationStatus = ReviewModerationStatus.Approved };
            var otherReview = new DirectoryEntryReviewComment { DirectoryEntryReviewId = 6, ModerationStatus = ReviewModerationStatus.Approved };
            var rejected = new DirectoryEntryReviewComment { DirectoryEntryReviewId = 5, ModerationStatus = ReviewModerationStatus.Rejected };

            Assert.True(SiteOwnerAdminController.IsValidParentComment(ok, 5));
            Assert.False(SiteOwnerAdminController.IsValidParentComment(otherReview, 5));
            Assert.False(SiteOwnerAdminController.IsValidParentComment(rejected, 5));
            Assert.False(SiteOwnerAdminController.IsValidParentComment(null, 5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a pgp key")]
        public void OwnerSession_IsInvalid_WhenListingKeyRemovedOrUnparseable(string? listingKey)
        {
            Assert.False(SiteOwnerAdminController.SessionKeyMatchesListingKey(
                "0123456789ABCDEF0123456789ABCDEF01234567", "0123456789ABCDEF0123456789ABCDEF01234567", listingKey));
        }

        // ---------- llms.txt wording from config ----------
        [Fact]
        public void LlmsClauses_ComeFromConfig_AndDisappearWhenUnset()
        {
            Assert.Equal(" that accept or relate to Monero (XMR)", LlmsController.AcceptOrRelateClause("Monero (XMR)"));
            Assert.Equal(" that accept Monero", LlmsController.AcceptShortClause("Monero (XMR)"));
            Assert.Equal(" that accept Bitcoin", LlmsController.AcceptShortClause("Bitcoin"));
            Assert.Equal(string.Empty, LlmsController.AcceptOrRelateClause(" "));
            Assert.Equal(string.Empty, LlmsController.AcceptShortClause(null));
        }

        private static SponsoredListingInvoice Invoice(decimal amountUsd, Currency paidIn, decimal paid) => new SponsoredListingInvoice
        {
            Amount = amountUsd,
            Currency = Currency.USD,
            PaidAmount = paid,
            PaidInCurrency = paidIn,
        };
    }
}
