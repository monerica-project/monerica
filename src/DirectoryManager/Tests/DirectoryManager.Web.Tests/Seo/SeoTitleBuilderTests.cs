using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Seo
{
    public class SeoTitleBuilderTests
    {
        private const string Site = "Monerica";
        private const string Currency = "Monero (XMR)";

        // A sample of real category/subcategory names, including the longest ones.
        public static IEnumerable<object[]> RealSubcategories() => new[]
        {
            new object[] { "Exchanges", "Instant Swaps" },
            new object[] { "Exchanges", "Aggregators" },
            new object[] { "Exchanges", "Peer-to-Peer Exchanges" },
            new object[] { "Wallets", "Mobile Wallets" },
            new object[] { "Mining", "Pools" },
            new object[] { "VPNs", "North America (Central America & Caribbean)" },
            new object[] { "Nodes", "Relay/Exit Operators (individual relays)" },
            new object[] { "Businesses", "Medical Testing & Laboratory Supplies" },
            new object[] { "Phone & SMS", "SMS Verification & Virtual Numbers" },
            new object[] { "Web Hosting", "Specialized or Niche Hosting Services" },
            new object[] { "Jobs & Freelancers", "Privacy & Security Consultants" },
            new object[] { "Media", "Talk Shows/ Podcasts (English)" },
        };

        [Fact]
        public void Listing_UsesFullFormWhenItFits()
        {
            var title = SeoTitleBuilder.ForListing("Trocador", "Aggregators", Currency, Site);
            Assert.Equal("Trocador: Monero Aggregator Reviews | Monerica", title);
        }

        [Fact]
        public void Listing_DropsBrandFirstThenReviews()
        {
            var name = "Some Very Long Exchange Name Here"; // 33 chars
            var title = SeoTitleBuilder.ForListing(name, "Instant Swaps", Currency, Site);
            Assert.Equal($"{name}: Monero Instant Swap", title);
            Assert.True(title.Length <= SeoTitleBuilder.MaxLength);

            var mid = "Cake Wallet Long Name";
            var midTitle = SeoTitleBuilder.ForListing(mid, "Mobile Wallets", Currency, Site);
            Assert.Equal($"{mid}: Monero Mobile Wallet Reviews", midTitle);
        }

        [Fact]
        public void Listing_NeverExceedsMaxLength_EvenForHugeNames()
        {
            var name = new string('x', 30) + " " + new string('y', 40);
            var title = SeoTitleBuilder.ForListing(name, "Mobile Wallets", Currency, Site);
            Assert.True(title.Length <= SeoTitleBuilder.MaxLength, title);
            Assert.NotEmpty(title);
        }

        [Fact]
        public void Listing_DoesNotRepeatCurrency()
        {
            var title = SeoTitleBuilder.ForListing("XMR Shop", "Monero Merchandise", Currency, Site);
            Assert.DoesNotContain("Monero Monero", title);
            Assert.StartsWith("XMR Shop: Monero Merchandise", title);
        }

        [Fact]
        public void Listing_PageSuffixIsAddedAndUnique()
        {
            var p1 = SeoTitleBuilder.ForListing("Trocador", "Aggregators", Currency, Site, 1);
            var p2 = SeoTitleBuilder.ForListing("Trocador", "Aggregators", Currency, Site, 2);
            Assert.NotEqual(p1, p2);
            Assert.Contains("(Page 2)", p2);
            Assert.True(p2.Length <= SeoTitleBuilder.MaxLength);
        }

        [Fact]
        public void Listing_WorksWithoutConfig()
        {
            Assert.Equal("Trocador: Aggregator Reviews", SeoTitleBuilder.ForListing("Trocador", "Aggregators", null, null));
            Assert.Equal("Trocador Reviews", SeoTitleBuilder.ForListing("Trocador", null, null, null));
        }

        [Fact]
        public void Subcategory_AddsCategoryContextWhenNameLacksIt()
        {
            Assert.Equal(
                "Instant Swaps: Monero (XMR) Exchanges | Monerica",
                SeoTitleBuilder.ForSubcategory("Instant Swaps", "Exchanges", Currency, Site));
        }

        [Fact]
        public void Subcategory_SkipsCategoryWhenNameAlreadyHasIt()
        {
            Assert.Equal(
                "Mobile Wallets for Monero (XMR) | Monerica",
                SeoTitleBuilder.ForSubcategory("Mobile Wallets", "Wallets", Currency, Site));
            Assert.Equal(
                "Tor & Onion Hosting for Monero (XMR) | Monerica",
                SeoTitleBuilder.ForSubcategory("Tor & Onion Hosting", "Web Hosting", Currency, Site));
        }

        [Fact]
        public void Subcategory_SameNameInTwoCategoriesGivesDifferentTitles()
        {
            var a = SeoTitleBuilder.ForSubcategory("Guides", "Mining", Currency, Site);
            var b = SeoTitleBuilder.ForSubcategory("Guides", "Nodes", Currency, Site);
            Assert.NotEqual(a, b);
        }

        [Theory]
        [MemberData(nameof(RealSubcategories))]
        public void Subcategory_RealNamesFitAndStartWithSubject(string category, string subcategory)
        {
            var title = SeoTitleBuilder.ForSubcategory(subcategory, category, Currency, Site);
            Assert.True(title.Length <= SeoTitleBuilder.MaxLength, $"{title} ({title.Length})");
            Assert.StartsWith(subcategory.Split(' ')[0], title);

            var page3 = SeoTitleBuilder.ForSubcategory(subcategory, category, Currency, Site, 3);
            Assert.True(page3.Length <= SeoTitleBuilder.MaxLength, $"{page3} ({page3.Length})");
        }

        [Fact]
        public void Category_IsKeywordFirst()
        {
            Assert.Equal("Exchanges for Monero (XMR) | Monerica", SeoTitleBuilder.ForCategory("Exchanges", Currency, Site));
            Assert.Equal("Exchanges for Monero (XMR) (Page 2) | Monerica", SeoTitleBuilder.ForCategory("Exchanges", Currency, Site, 2));
        }

        [Fact]
        public void WithBrand_AddsBrandOnlyWhenItFitsAndIsMissing()
        {
            Assert.Equal("FAQ | Monerica", SeoTitleBuilder.WithBrand("FAQ", Site));
            Assert.Equal("About Monerica", SeoTitleBuilder.WithBrand("About Monerica", Site));
            var long55 = new string('a', 55);
            Assert.Equal(long55, SeoTitleBuilder.WithBrand(long55, Site));
            Assert.Equal("Monerica", SeoTitleBuilder.WithBrand(null, Site));
            Assert.Equal("FAQ", SeoTitleBuilder.WithBrand("FAQ", null));
        }

        [Theory]
        [InlineData("Instant Swaps", "Instant Swap")]
        [InlineData("Communities", "Community")]
        [InlineData("VPNs", "VPN")]
        [InlineData("ATMs", "ATM")]
        [InlineData("Peer-to-Peer Exchanges", "Peer-to-Peer Exchange")]
        [InlineData("Miscellaneous", "Miscellaneous")]
        [InlineData("Fitness", "Fitness")]
        [InlineData("Statistics", "Statistics")]
        [InlineData("News (English)", "News (English)")]
        [InlineData("Accounting", "Accounting")]
        [InlineData("Proxies", "Proxy")]
        [InlineData("Business Directories", "Business Directory")]
        public void Singularize_LastWord(string input, string expected)
        {
            Assert.Equal(expected, SeoTitleBuilder.Singularize(input));
        }

        [Fact]
        public void ShortCurrency_StripsTicker()
        {
            Assert.Equal("Monero", SeoTitleBuilder.ShortCurrency("Monero (XMR)"));
            Assert.Equal("Bitcoin", SeoTitleBuilder.ShortCurrency("Bitcoin"));
            Assert.Equal(string.Empty, SeoTitleBuilder.ShortCurrency(null));
        }

        [Fact]
        public void Truncate_CutsAtWordBoundary()
        {
            var cut = SeoTitleBuilder.Truncate("alpha beta gamma delta epsilon", 17);
            Assert.Equal("alpha beta gamma", cut);
        }
    }
}
