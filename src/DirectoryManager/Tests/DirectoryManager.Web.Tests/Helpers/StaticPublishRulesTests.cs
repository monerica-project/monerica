using System.Net;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.StaticPublishing;
using Microsoft.AspNetCore.Http;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>Publish on change: which pages a change touches, what a manual republish accepts,
    /// and who may call the internal publisher endpoints.</summary>
    public class StaticPublishRulesTests
    {
        private const string Secret = "0123456789abcdef0123456789abcdef";

        [Fact]
        public void ListingChange_TouchesItsProfileSubcategoryCategoryAndSiteWidePages_ListingFirst()
        {
            var paths = StaticPublishPathMap.ForListing(new ListingPageFacts
            {
                EntryKey = "offcard",
                Placements = { Placement("exchanges", 20, "instant-swaps", 10) },
            });

            Assert.Equal("/site/offcard", paths[0]);
            Assert.Contains("/exchanges/instant-swaps", paths);
            Assert.Contains("/exchanges", paths);
            foreach (var p in new[] { "/", "/newest", "/reviews", "/incidents", "/sponsors", "/sitemap.xml", "/rss/feed.xml", "/llms.txt", "/llms-full.txt", "/data.json", "/feed.json" })
            {
                Assert.Contains(p, paths);
            }

            Assert.DoesNotContain(paths, p => p.Contains("/page/", StringComparison.Ordinal));
            Assert.Equal(paths.Count, paths.Distinct().Count());
        }

        [Fact]
        public void PagedLists_IncludeEveryPage_PlusOneThatMayHaveJustDisappeared()
        {
            var paths = StaticPublishPathMap.ForListing(new ListingPageFacts
            {
                EntryKey = "x",
                CrowdReviewCount = 25, // 10 per page → 3 pages (+1 slack = none: ceil(26/10)=3)
                Placements = { Placement("businesses", 101, "shops", 50) },
            });

            Assert.Contains("/site/x/page/2", paths);
            Assert.Contains("/site/x/page/3", paths);
            Assert.DoesNotContain("/site/x/page/4", paths);

            // 50 per page: 50 listings fill page 1; page 2 is the "just disappeared" check.
            Assert.Contains("/businesses/shops/page/2", paths);
            Assert.DoesNotContain("/businesses/shops/page/3", paths);

            // 101 listings → 3 pages, and ceil(102/50) = 3.
            Assert.Contains("/businesses/page/3", paths);
            Assert.DoesNotContain("/businesses/page/4", paths);
        }

        [Fact]
        public void AdditionalSubcategories_AreRepublishedToo()
        {
            var paths = StaticPublishPathMap.ForListing(new ListingPageFacts
            {
                EntryKey = "x",
                Placements =
                {
                    Placement("exchanges", 5, "instant-swaps", 5),
                    Placement("wallets", 5, "mobile", 5),
                },
            });

            Assert.Contains("/exchanges/instant-swaps", paths);
            Assert.Contains("/wallets/mobile", paths);
            Assert.Contains("/wallets", paths);
        }

        [Fact]
        public void MovedListing_RepublishesTheOldAndTheNewPages()
        {
            var before = StaticPublishPathMap.ForListing(new ListingPageFacts
            {
                EntryKey = "old-name",
                Placements = { Placement("exchanges", 5, "instant-swaps", 5) },
                Tags = { new PagedPageFacts { Slug = "no-kyc", EntryCount = 3 } },
                TagCount = 1,
                Country = new PagedPageFacts { Slug = "canada", EntryCount = 2 },
                CountryCount = 1,
            });
            var after = StaticPublishPathMap.ForListing(new ListingPageFacts
            {
                EntryKey = "new-name",
                Placements = { Placement("wallets", 5, "desktop", 5) },
            });

            var all = StaticPublishPathMap.Union(before, after);

            foreach (var p in new[] { "/site/old-name", "/site/new-name", "/exchanges/instant-swaps", "/exchanges", "/wallets/desktop", "/wallets", "/tagged/no-kyc", "/tagged", "/countries/canada", "/countries" })
            {
                Assert.Contains(p, all);
            }

            Assert.Equal(all.Count, all.Distinct().Count());
        }

        [Fact]
        public void TagsAndCountry_AddTheirPagesAndIndexes()
        {
            var paths = StaticPublishPathMap.ForListing(new ListingPageFacts
            {
                EntryKey = "x",
                Tags = { new PagedPageFacts { Slug = "tor", EntryCount = 150 } },
                TagCount = 250,
                Country = new PagedPageFacts { Slug = "united-states", EntryCount = 20 },
                CountryCount = 120,
            });

            Assert.Contains("/tagged/tor", paths);
            Assert.Contains("/tagged/tor/page/2", paths);
            Assert.Contains("/tagged/page/3", paths);
            Assert.Contains("/countries/united-states", paths);
            Assert.Contains("/countries/page/2", paths);
        }

        [Theory]
        [InlineData("/site/abc", "/site/abc")]
        [InlineData("site/abc/", "/site/abc")]
        [InlineData("https://monerica.com/site/abc", "/site/abc")]
        [InlineData("https://app.monerica.com/exchanges/instant-swaps/page/2", "/exchanges/instant-swaps/page/2")]
        [InlineData("https://monerica.com/site/abc#reviews", "/site/abc")]
        [InlineData("/", "/")]
        [InlineData("https://monerica.com", "/")]
        [InlineData("/sitemap.xml", "/sitemap.xml")]
        public void ManualRepublish_AcceptsLocalPagesInAnyCommonForm(string input, string expected)
        {
            Assert.True(StaticPublishPathRules.TryNormalize(input, Hosts, out var path, out var error), error);
            Assert.Equal(expected, path);
        }

        [Theory]
        [InlineData("")]
        [InlineData("https://evil.example/site/abc")]
        [InlineData("//evil.example/site/abc")]
        [InlineData("javascript:alert(1)")]
        [InlineData("/site/../etc/passwd")]
        [InlineData("/site//abc")]
        [InlineData("/site/abc?x=1")]
        [InlineData("https://monerica.com/search?q=x")]
        [InlineData("/site/a%2e%2e")]
        [InlineData("/site/a b")]
        [InlineData("/site/<script>")]
        [InlineData("*")]
        [InlineData("ftp://monerica.com/x")]
        public void ManualRepublish_RejectsAnythingThatIsNotAPlainLocalPage(string input)
        {
            Assert.False(StaticPublishPathRules.TryNormalize(input, Hosts, out _, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void InternalEndpoint_AllowsOnlyLoopback_OnTheInternalPort_WithTheSecret()
        {
            var o = Options();

            Assert.True(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Static-Publish-Key", Secret)), o, out _));
            Assert.True(InternalEndpointGuard.IsAllowed(5056, IPAddress.IPv6Loopback, Headers(("X-Static-Publish-Key", Secret)), o, out _));
        }

        [Fact]
        public void InternalEndpoint_RejectsMissingOrWrongSecret()
        {
            var o = Options();

            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(), o, out var r1));
            Assert.Equal("missing or wrong secret", r1);
            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Static-Publish-Key", Secret + "x")), o, out _));
            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Other", Secret)), o, out _));
        }

        [Fact]
        public void InternalEndpoint_RejectsNonLoopback_PublicPort_AndProxiedRequests()
        {
            var o = Options();
            var good = Headers(("X-Static-Publish-Key", Secret));

            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Parse("203.0.113.9"), good, o, out _));
            Assert.False(InternalEndpointGuard.IsAllowed(5056, null, good, o, out _));
            Assert.False(InternalEndpointGuard.IsAllowed(5055, IPAddress.Loopback, good, o, out var port));
            Assert.Equal("not on the internal port", port);
            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Static-Publish-Key", Secret), ("X-Forwarded-For", "1.2.3.4")), o, out var proxied));
            Assert.Equal("request came through a proxy", proxied);
            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Static-Publish-Key", Secret), ("X-Real-IP", "1.2.3.4")), o, out _));
        }

        [Fact]
        public void InternalEndpoint_IsClosed_WhenNotConfigured_OrTheSecretIsWeak()
        {
            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Static-Publish-Key", Secret)), new StaticPublishOptions(), out _));

            var weak = new StaticPublishOptions { InternalPort = 5056, InternalSecret = "short" };
            Assert.False(weak.InternalEndpointEnabled);
            Assert.False(InternalEndpointGuard.IsAllowed(5056, IPAddress.Loopback, Headers(("X-Static-Publish-Key", "short")), weak, out _));
        }

        private static readonly string[] Hosts = { "monerica.com", "app.monerica.com" };

        private static ListingPlacementFacts Placement(string cat, int catCount, string sub, int subCount) =>
            new ListingPlacementFacts { CategoryKey = cat, CategoryEntryCount = catCount, SubcategoryKey = sub, SubcategoryEntryCount = subCount };

        private static StaticPublishOptions Options() =>
            new StaticPublishOptions { InternalPort = 5056, InternalSecret = Secret, Mode = StaticPublishMode.On };

        private static IHeaderDictionary Headers(params (string Name, string Value)[] values)
        {
            var h = new HeaderDictionary();
            foreach (var (name, value) in values)
            {
                h[name] = value;
            }

            return h;
        }
    }
}
