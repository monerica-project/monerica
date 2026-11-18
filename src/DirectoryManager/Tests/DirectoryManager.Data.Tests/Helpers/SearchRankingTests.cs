using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Tests.Helpers
{
    /// <summary>
    /// The quality-aware ranking model (SearchRankingModel), query normalization, synonyms,
    /// typo fallback and the category links shown above results.
    /// </summary>
    public class SearchRankingTests
    {
        private static readonly DateTime Now = new (2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void BrandName_IsFirst_EvenWhenAnotherListingHasManyMoreReviews()
        {
            var dir = new List<DirectoryEntry>
            {
                E(1, "SwapKing", "Instant swap service. Better than Nimbus.", "Instant Swaps", "Exchanges", DirectoryStatus.Verified),
                E(2, "Nimbus", "Instant swaps.", "Instant Swaps", "Exchanges", DirectoryStatus.Admitted),
            };
            var signals = Signals((1, 60, 4.9));

            Assert.Equal("Nimbus", Rank(dir, signals, "nimbus").First());
        }

        [Fact]
        public void EqualTextMatch_VerifiedWellReviewedListingRanksFirst()
        {
            var dir = new List<DirectoryEntry>
            {
                E(1, "Alpha Host", "Servers.", "VPS Hosting", "Web Hosting", DirectoryStatus.Admitted),
                E(2, "Beta Host", "Servers.", "VPS Hosting", "Web Hosting", DirectoryStatus.Verified),
                E(3, "Gamma Host", "Servers.", "VPS Hosting", "Web Hosting", DirectoryStatus.Verified),
            };
            var signals = Signals((3, 12, 4.8));

            Assert.Equal(new[] { "Gamma Host", "Beta Host", "Alpha Host" }, Rank(dir, signals, "vps"));
        }

        [Fact]
        public void OneFiveStarReview_DoesNotBeatManyStrongReviews()
        {
            var dir = new List<DirectoryEntry>
            {
                E(1, "One Review", "Gift cards.", "Gift Cards", "Cards", DirectoryStatus.Verified),
                E(2, "Forty Reviews", "Gift cards.", "Gift Cards", "Cards", DirectoryStatus.Verified),
            };
            var signals = Signals((1, 1, 5.0), (2, 40, 4.6));

            Assert.Equal("Forty Reviews", Rank(dir, signals, "gift cards").First());

            var one = SearchRankingModel.Compute(dir[0], signals);
            var forty = SearchRankingModel.Compute(dir[1], signals);
            Assert.True(forty.Total > one.Total);
        }

        [Fact]
        public void Questionable_SinksBelowOtherMatches()
        {
            var dir = new List<DirectoryEntry>
            {
                E(1, "Shady Swap", "Instant swaps.", "Instant Swaps", "Exchanges", DirectoryStatus.Questionable),
                E(2, "Plain Swap", "Instant swaps.", "Instant Swaps", "Exchanges", DirectoryStatus.Admitted),
            };
            var signals = Signals((1, 30, 4.9));

            Assert.Equal(new[] { "Plain Swap", "Shady Swap" }, Rank(dir, signals, "swap"));
        }

        [Fact]
        public void OpenProblemReports_LowerAListingWithinItsTier()
        {
            var dir = new List<DirectoryEntry>
            {
                E(1, "Reported VPN", "VPN service.", "Outside the 14 Eyes", "VPNs & Proxies", DirectoryStatus.Verified),
                E(2, "Clean VPN", "VPN service.", "Outside the 14 Eyes", "VPNs & Proxies", DirectoryStatus.Verified),
            };
            var signals = new SearchSignals
            {
                ByEntryId = new Dictionary<int, SearchListingSignals> { [1] = new () { OpenProblemReports = 2 } },
                NowUtc = Now,
            };

            Assert.Equal(new[] { "Clean VPN", "Reported VPN" }, Rank(dir, signals, "vpn"));
        }

        [Fact]
        public void RepeatingAWordInTheDescription_DoesNotRaiseAListing()
        {
            var dir = new List<DirectoryEntry>
            {
                E(1, "Aardvark Cloud", "Storage.", "Storage & Seedboxes", "Web Hosting", DirectoryStatus.Admitted),
                E(2, "Zebra Cloud", "Storage storage storage storage storage, the best storage.", "Storage & Seedboxes", "Web Hosting", DirectoryStatus.Admitted),
            };

            var results = DirectorySearchEngine.RankExplained(DirectorySearchEngine.BuildIndex(dir), "storage");

            Assert.Equal(results[0].Relevance, results[1].Relevance);
            Assert.Equal("Aardvark Cloud", results[0].Entry.Name); // equal score → name order
        }

        [Fact]
        public void Ordering_IsDeterministic_RegardlessOfInputOrder()
        {
            var dir = Enumerable.Range(1, 12)
                .Select(i => E(i, $"Host {i:00}", "VPS servers.", "VPS Hosting", "Web Hosting", i % 3 == 0 ? DirectoryStatus.Verified : DirectoryStatus.Admitted))
                .ToList();
            var signals = Signals((3, 5, 4.5), (6, 5, 4.5), (7, 2, 3.0));

            var a = Rank(dir, signals, "vps");
            var b = Rank(Enumerable.Reverse(dir).ToList(), signals, "vps");

            Assert.Equal(a, b);
        }

        [Fact]
        public void GenericSearch_NameThatOnlyContainsTheWord_DoesNotBeatListingsOfThatKind()
        {
            // "VPS Price Tracker" (a directory) used to rank above VPS hosts for "vps".
            var dir = new List<DirectoryEntry>
            {
                E(1, "VPS Price Tracker", "Compares prices.", "Miscellaneous", "Other Directories", DirectoryStatus.Admitted),
                E(2, "Solid Hosting", "Virtual servers.", "VPS Hosting", "Web Hosting", DirectoryStatus.Verified),
            };

            Assert.Equal("Solid Hosting", Rank(dir, SearchSignals.None, "vps").First());
        }

        [Fact]
        public void CategoryOnlyMatch_RanksBelowSubcategoryMatch()
        {
            // Every VPN is in "VPNs & Proxies"; for "proxy" the Proxies subcategory comes first.
            var dir = new List<DirectoryEntry>
            {
                E(1, "Famous VPN", "VPN.", "Outside the 14 Eyes", "VPNs & Proxies", DirectoryStatus.Verified),
                E(2, "Little Proxies", "Residential proxies.", "Proxies", "VPNs & Proxies", DirectoryStatus.Admitted),
            };

            Assert.Equal("Little Proxies", Rank(dir, Signals((1, 50, 5.0)), "proxy").First());
        }

        [Fact]
        public void GenericSearch_SubcategoryNamedExactly_BeatsOneThatOnlyContainsTheWord()
        {
            // "proxy": the Proxies subcategory, not Proxy Shopping, even with fewer reviews.
            var dir = new List<DirectoryEntry>
            {
                E(1, "Shop For Me", "We buy things for you.", "Proxy Shopping", "Businesses", DirectoryStatus.Verified),
                E(2, "Residential IPs", "Rotating proxies.", "Proxies", "VPNs & Proxies", DirectoryStatus.Verified),
            };

            Assert.Equal("Residential IPs", Rank(dir, Signals((1, 3, 5.0), (2, 1, 5.0)), "proxy").First());
        }

        [Theory]
        [InlineData("e-sim")]
        [InlineData("e sim")]
        [InlineData("E.SIM")]
        [InlineData("esim")]
        [InlineData("simcard")]
        public void Normalization_EsimVariants_FindTheEsimListing(string query)
        {
            Assert.Equal("Cypher eSIM", Rank(Basic(), SearchSignals.None, query).First());
        }

        [Fact]
        public void Normalization_NoKycVariants_AreTheSame()
        {
            var a = Rank(Basic(), SearchSignals.None, "no kyc vps");
            Assert.Equal(a, Rank(Basic(), SearchSignals.None, "no-kyc vps"));
            Assert.Equal(a, Rank(Basic(), SearchSignals.None, "nokyc vps"));
            Assert.Equal(a, Rank(Basic(), SearchSignals.None, "non-kyc vps"));
        }

        [Fact]
        public void Normalization_GiftCardVariants_AreTheSameListings()
        {
            var a = Rank(Basic(), SearchSignals.None, "gift card");
            Assert.Contains("Coincards", a);
            Assert.Contains("Coincards", Rank(Basic(), SearchSignals.None, "gift-card"));
            Assert.Contains("Coincards", Rank(Basic(), SearchSignals.None, "giftcard"));
        }

        [Fact]
        public void Normalization_AccentsAreFolded()
        {
            Assert.Equal("Trêvoid Crypto Swaps", Rank(Basic(), SearchSignals.None, "trevoid").First());
            Assert.Equal("Cypher eSIM", Rank(Basic(), SearchSignals.None, "esım").First()); // Turkish dotless i
        }

        [Theory]
        [InlineData("casino", "Lucky Dice")]
        [InlineData("credit card", "Plastic Cards")]
        [InlineData("btc", "Bitcoin Corner")]
        [InlineData("p2p", "Trêvoid Crypto Swaps")] // both P2P; Verified first
        public void Synonyms_FindTheSameThing(string query, string expected)
        {
            Assert.Equal(expected, Rank(Basic(), SearchSignals.None, query).First());
        }

        [Theory]
        [InlineData("buy monero")]
        [InlineData("buy xmr")]
        [InlineData("buy crypto no kyc")]
        [InlineData("on ramp")]
        public void BuyMonero_ShowsExchanges(string query)
        {
            // Every word is a stop word; these used to match random listings named "Buy …".
            var results = DirectorySearchEngine.Rank(DirectorySearchEngine.BuildIndex(Basic()), query);

            Assert.NotEmpty(results);
            Assert.All(results, e => Assert.Equal("Exchanges", e.SubCategory!.Category!.Name));
        }

        [Fact]
        public void StopWords_AreIgnoredWhenOtherWordsRemain()
        {
            Assert.Equal(Rank(Basic(), SearchSignals.None, "vps"), Rank(Basic(), SearchSignals.None, "monero vps"));
            Assert.Equal(Rank(Basic(), SearchSignals.None, "esim"), Rank(Basic(), SearchSignals.None, "buy esim with xmr"));
        }

        [Fact]
        public void Typo_InsideASquashedName_IsCorrected()
        {
            // "bazar" (4 searches) used to find nothing; XMRBazaar is "xmr" + "bazaar".
            Assert.Empty(Rank(Basic(), SearchSignals.None, "bazar"));
            var c = DirectorySearchEngine.SuggestCorrection(Basic(), "bazar");
            Assert.NotNull(c);
            Assert.Equal("bazaar", c!.Query);
            Assert.True(c.AutoApply);
            Assert.Equal("XMRBazaar", Rank(Basic(), SearchSignals.None, c.Query).First());
        }

        [Fact]
        public void Typo_ShortWord_UsesOneEdit_LongWord_UsesTwo()
        {
            Assert.Null(DirectorySearchEngine.SuggestCorrection(Basic(), "travala")); // 7 letters, 2 edits from "travel"
            Assert.Equal("trocador", DirectorySearchEngine.SuggestCorrection(Basic(), "trocadr")!.Query);
            Assert.Equal("xmrbazaar", DirectorySearchEngine.SuggestCorrection(Basic(), "xmrbazzaar")!.Query);
        }

        [Fact]
        public void Scam_ShownOnlyWhenNamed()
        {
            Assert.DoesNotContain("Swap Thief", Rank(Basic(), SearchSignals.None, "swap"));
            Assert.Equal("Swap Thief", Rank(Basic(), SearchSignals.None, "swap thief").First());
        }

        [Fact]
        public void BrowseLinks_SearchNamingACategory_LinksToIt()
        {
            var targets = Targets();

            Assert.Equal("/vpns", SearchBrowseLinks.FindNamed("vpn", targets).First().Path);
            Assert.Equal("/cards/gift-cards", SearchBrowseLinks.FindNamed("gift cards", targets).First().Path);
            Assert.Equal("/phone-and-sms/sms-verification", SearchBrowseLinks.FindNamed("sms", targets).First().Path);
            Assert.Equal("/businesses/gambling", SearchBrowseLinks.FindNamed("casino", targets).First().Path);
            Assert.Equal("/countries/russia", SearchBrowseLinks.FindNamed("russia", targets).First().Path);
            Assert.Equal("/phone-and-sms/esims", SearchBrowseLinks.FindNamed("e-sim", targets).First().Path);
            Assert.Empty(SearchBrowseLinks.FindNamed("trocador", targets));
        }

        [Fact]
        public void BrowseLinks_ZeroResults_SuggestClosestCategories()
        {
            Assert.Contains(SearchBrowseLinks.FindClosest("cheap hosting deals", Targets()), t => t.Path == "/web-hosting");
            Assert.Empty(SearchBrowseLinks.FindClosest("xyzzy", Targets()));
        }

        [Theory]
        [InlineData("advertise", "/advertise")]
        [InlineData("Advertising", "/advertise")]
        [InlineData("scam", "/incidents")]
        [InlineData("submit", "/submit")]
        public void Shortcuts_NavigationalSearches(string query, string path)
        {
            Assert.Equal(path, SearchBrowseLinks.FindShortcut(query)!.Path);
            Assert.Null(SearchBrowseLinks.FindShortcut("vps"));
        }

        private static List<BrowseTarget> Targets() => new ()
        {
            new ("VPNs & Proxies", "/vpns", SearchBrowseLinks.TargetKind.Category, "VPNs & Proxies"),
            new ("Proxies", "/vpns/proxies", SearchBrowseLinks.TargetKind.Subcategory, "Proxies"),
            new ("Gift Cards", "/cards/gift-cards", SearchBrowseLinks.TargetKind.Subcategory, "Gift Cards"),
            new ("Cards", "/cards", SearchBrowseLinks.TargetKind.Category, "Cards"),
            new ("SMS", "/phone-and-sms/sms-verification", SearchBrowseLinks.TargetKind.Subcategory, "SMS Verification & Virtual Numbers"),
            new ("eSIMs", "/phone-and-sms/esims", SearchBrowseLinks.TargetKind.Subcategory, "eSIMs & SIM Cards"),
            new ("Gambling", "/businesses/gambling", SearchBrowseLinks.TargetKind.Subcategory, "Gambling"),
            new ("Web Hosting", "/web-hosting", SearchBrowseLinks.TargetKind.Category, "Web Hosting"),
            new ("Listings in Russia", "/countries/russia", SearchBrowseLinks.TargetKind.Country, "RU"),
        };

        private static List<DirectoryEntry> Basic() => new ()
        {
            E(1, "Cypher eSIM", "eSIM data plans.", "eSIMs & SIM Cards", "Phone & SMS", DirectoryStatus.Verified),
            E(2, "Coincards", "Gift cards for Amazon.", "Gift Cards", "Cards", DirectoryStatus.Verified),
            E(3, "BTCVPS", "VPS hosting.", "VPS Hosting", "Web Hosting", DirectoryStatus.Verified, KycPolicy.GuaranteedNoKyc),
            E(4, "KYC Host", "VPS hosting with ID.", "VPS Hosting", "Web Hosting", DirectoryStatus.Verified, KycPolicy.MandatoryKyc),
            E(5, "Trêvoid Crypto Swaps", "Peer trading.", "Peer-to-Peer Exchanges", "Exchanges", DirectoryStatus.Verified),
            E(6, "Lucky Dice", "Dice games.", "Gambling", "Businesses", DirectoryStatus.Verified),
            E(7, "Quick Swap", "Instant swaps.", "Instant Swaps", "Exchanges", DirectoryStatus.Verified, KycPolicy.GuaranteedNoKyc),
            E(8, "Plastic Cards", "Virtual cards.", "Prepaid Cards", "Cards", DirectoryStatus.Verified),
            E(9, "Bitcoin Corner", "A Bitcoin shop.", "Miscellaneous", "Businesses", DirectoryStatus.Admitted),
            E(10, "Trade Floor", "Trade with people.", "Peer-to-Peer Exchanges", "Exchanges", DirectoryStatus.Admitted),
            E(11, "XMRBazaar", "Marketplace.", "Marketplaces", "Businesses", DirectoryStatus.Verified),
            E(12, "Trocador", "Swap aggregator.", "Aggregators", "Exchanges", DirectoryStatus.Verified),
            E(13, "Swap Thief", "Instant swaps.", "Instant Swaps", "Exchanges", DirectoryStatus.Scam),
            E(14, "Liberation Travel", "Travel agency.", "Travel", "Businesses", DirectoryStatus.Verified),
        };

        private static List<string> Rank(List<DirectoryEntry> dir, SearchSignals signals, string q) =>
            DirectorySearchEngine.Rank(DirectorySearchEngine.BuildIndex(dir, signals), q).Select(e => e.Name).ToList();

        private static SearchSignals Signals(params (int Id, int Count, double Avg)[] reviews) => new ()
        {
            ByEntryId = reviews.ToDictionary(
                r => r.Id,
                r => new SearchListingSignals { ReviewCount = r.Count, AverageRating = r.Avg, LastReviewUtc = Now.AddDays(-30) }),
            SiteMeanRating = 4.3,
            NowUtc = Now,
        };

        private static DirectoryEntry E(int id, string name, string description, string subcategory, string category, DirectoryStatus status, KycPolicy? kyc = null) => new ()
        {
            DirectoryEntryId = id,
            Name = name,
            DirectoryEntryKey = $"k{id}",
            Description = description,
            Link = $"https://site{id}.example",
            DirectoryStatus = status,
            KycPolicy = kyc,
            SubCategory = new Subcategory
            {
                Name = subcategory,
                SubCategoryKey = subcategory.ToLowerInvariant(),
                Category = new Category { Name = category, CategoryKey = category.ToLowerInvariant() },
            },
            EntryTags = new List<DirectoryEntryTag>(),
        };
    }
}
