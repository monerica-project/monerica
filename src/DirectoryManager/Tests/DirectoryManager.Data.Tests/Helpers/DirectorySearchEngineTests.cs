using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Tests.Helpers
{
    /// <summary>
    /// Cases taken from real searches on the site that used to give bad results.
    /// </summary>
    public class DirectorySearchEngineTests
    {
        private static readonly List<DirectoryEntry> Directory = new ()
        {
            Entry(1, "GoyMail", "Private email service. Pay with Monero.", "Email Providers", "Communication", DirectoryStatus.Verified),
            Entry(2, "Mailum", "Encrypted mail hosting.", "Email Providers", "Communication", DirectoryStatus.Verified),
            Entry(3, "NanoGPT", "Pay-per-prompt access to AI models like Claude and GPT.", "AI Services", "Services", DirectoryStatus.Admitted),
            Entry(4, "MyCryptoCheckout", "Crypto payments plugin for WordPress.", "Payment Plugins", "Merchant Services", DirectoryStatus.Verified),
            Entry(5, "Oudel", "Windows RDP and VPS hosting.", "VPS and Dedicated Server Providers", "Web Hosting", DirectoryStatus.Admitted, kyc: KycPolicy.GuaranteedNoKyc),
            Entry(6, "BTCVPS", "VPS with storage add-ons. No account required.", "VPS and Dedicated Server Providers", "Web Hosting", DirectoryStatus.Verified, kyc: KycPolicy.GuaranteedNoKyc),
            Entry(7, "KYC Host", "VPS hosting that requires ID.", "VPS and Dedicated Server Providers", "Web Hosting", DirectoryStatus.Verified, kyc: KycPolicy.MandatoryKyc),
            Entry(8, "Coincards", "Gift cards for Amazon, Uber and more.", "Gift Cards", "Cards", DirectoryStatus.Verified, cc: "CA"),
            Entry(9, "Cypher eSIM", "eSIM data plans for the USA and 180 countries.", "eSIM Providers", "Phones", DirectoryStatus.Admitted),
            Entry(10, "Silent eSIM", "Travel eSIM for Europe.", "eSIM Providers", "Phones", DirectoryStatus.Admitted),
            Entry(11, "Matthew Ryan, CPA.", "Accounting services.", "Accountants", "Businesses", DirectoryStatus.Verified, cc: "US"),
            Entry(12, "Trocador", "Swap aggregator.", "Aggregators", "Exchanges", DirectoryStatus.Verified, link: "https://trocador.app"),
            Entry(13, "Mullvad", "VPN that accepts Monero.", "VPNs", "Privacy", DirectoryStatus.Admitted),
            Entry(14, "SMS Activate", "SMS verification numbers.", "SMS Verification", "Phones", DirectoryStatus.Scam),
            Entry(15, "SMSPVA", "SMS verification numbers.", "SMS Verification", "Phones", DirectoryStatus.Admitted),
            Entry(16, "Bitania", "Instant exchange.", "Instant Swaps", "Exchanges", DirectoryStatus.Verified),
            Entry(17, "XMR.CARDS", "Prepaid cards.", "Prepaid Cards", "Cards", DirectoryStatus.Admitted, link: "https://xmr.cards"),
            Entry(18, "XMR.BAR", "Dice game.", "Gambling", "Businesses", DirectoryStatus.Admitted, link: "https://xmr.bar"),
            Entry(19, "Monero Miner", "Mining software.", "Mining Software", "Mining", DirectoryStatus.Admitted),
            Entry(20, "Reagent Tests UK", "Testing kits shipped in the UK.", "Health", "Businesses", DirectoryStatus.Admitted, cc: "GB"),
        };

        [Fact]
        public void ShortWord_OnlyMatchesWholeWords()
        {
            // "ai" used to match "m-ai-l" and rank GoyMail/Mailum first.
            var names = Search("ai");

            Assert.Equal("NanoGPT", names.First());
            Assert.DoesNotContain("GoyMail", names);
            Assert.DoesNotContain("Mailum", names);
        }

        [Fact]
        public void ShortWord_DoesNotMatchInsideLongerWord()
        {
            // "rdp" is inside "wordpress".
            var names = Search("rdp");

            Assert.Equal("Oudel", names.First());
            Assert.DoesNotContain("MyCryptoCheckout", names);
        }

        [Fact]
        public void ShortWord_WithNoWholeWordMatch_FallsBackToNameStart()
        {
            Assert.Equal("Bitania", Search("bit").First());
        }

        [Fact]
        public void MultipleWords_MatchInAnyOrder()
        {
            var a = Search("vps no kyc");
            var b = Search("no kyc vps");

            Assert.Equal(a, b);
            Assert.Contains("BTCVPS", a);
            Assert.Contains("Oudel", a);
        }

        [Fact]
        public void NoKyc_ExcludesListingsThatRequireId()
        {
            Assert.DoesNotContain("KYC Host", Search("no kyc vps"));
        }

        [Fact]
        public void MultipleWords_AllMustMatch()
        {
            var names = Search("amazon gift card");

            Assert.Equal(new[] { "Coincards" }, names);
        }

        [Fact]
        public void NameMatch_BeatsPassingMentionInDescription()
        {
            // BTCVPS (Verified) mentions "storage"; it must not outrank a listing named for the term.
            var names = Search("vps");

            Assert.Equal("BTCVPS", names.First());
            Assert.True(names.IndexOf("Oudel") >= 0);
        }

        [Fact]
        public void Country_NarrowsOtherWordsInsteadOfReturningEveryListingThere()
        {
            // "esim usa" used to return every US listing (an accountant first).
            var names = Search("esim usa");

            Assert.Equal(new[] { "Cypher eSIM" }, names);
        }

        [Fact]
        public void CountryAlone_ListsThatCountry_NamedListingsFirst()
        {
            var names = Search("uk");

            Assert.Equal("Reagent Tests UK", names.First());
            Assert.DoesNotContain("Matthew Ryan, CPA.", names);
        }

        [Fact]
        public void TwoLetterWord_IsNotTreatedAsCountryCode()
        {
            // "ai" is also Anguilla's country code.
            Assert.NotEmpty(Search("ai"));
        }

        [Fact]
        public void Scam_SinksBelowOtherListings()
        {
            var names = Search("sms");

            Assert.Equal("SMSPVA", names.First());
            Assert.Equal("SMS Activate", names.Last());
        }

        [Fact]
        public void Scam_StillFirstWhenSearchedByExactName()
        {
            Assert.Equal("SMS Activate", Search("sms activate").First());
        }

        [Fact]
        public void Domain_ShowsOnlyThatSite()
        {
            Assert.Equal(new[] { "XMR.CARDS" }, Search("xmr.cards"));
            Assert.Equal("Trocador", Search("https://trocador.app").First());
        }

        [Fact]
        public void StopWords_AreIgnoredWhenOtherWordsRemain()
        {
            Assert.Equal(Search("esim"), Search("buy esim"));
            Assert.Equal(Search("vps"), Search("monero vps"));
        }

        [Fact]
        public void Plural_MatchesSingular()
        {
            Assert.Contains("Coincards", Search("gift card"));
            Assert.Contains("Coincards", Search("gift cards"));
        }

        [Theory]
        [InlineData("trocadr", "trocador")]
        [InlineData("mulvad", "mullvad")]
        public void Typo_IsAutoCorrected(string typed, string expected)
        {
            Assert.Empty(Search(typed));

            var correction = DirectorySearchEngine.SuggestCorrection(Directory, typed);

            Assert.NotNull(correction);
            Assert.Equal(expected, correction!.Query);
            Assert.True(correction.AutoApply);
        }

        [Fact]
        public void ChangedLetter_IsOnlySuggested()
        {
            // "mixer" → "miner" is a different real word, so don't swap results silently.
            var correction = DirectorySearchEngine.SuggestCorrection(Directory, "mixer");

            Assert.NotNull(correction);
            Assert.Equal("miner", correction!.Query);
            Assert.False(correction.AutoApply);
        }

        [Fact]
        public void ScamNames_AreNeverSuggested()
        {
            var correction = DirectorySearchEngine.SuggestCorrection(Directory, "activat");

            Assert.True(correction == null || !correction.Query.Contains("activate"));
        }

        [Fact]
        public void ExactName_IsFirst()
        {
            Assert.Equal("Trocador", Search("trocador").First());
            Assert.Equal("GoyMail", Search("goymail").First());
        }

        [Fact]
        public void EmptyQuery_ReturnsNothing()
        {
            Assert.Empty(Search("   "));
        }

        private static List<string> Search(string q) =>
            DirectorySearchEngine.Rank(Directory, q).Select(e => e.Name).ToList();

        private static DirectoryEntry Entry(
            int id,
            string name,
            string description,
            string subcategory,
            string category,
            DirectoryStatus status,
            KycPolicy? kyc = null,
            string? cc = null,
            string? link = null)
        {
            return new DirectoryEntry
            {
                DirectoryEntryId = id,
                Name = name,
                DirectoryEntryKey = $"k{id}",
                Description = description,
                Link = link ?? $"https://{name.ToLowerInvariant().Replace(" ", string.Empty).Replace(",", string.Empty)}.example",
                DirectoryStatus = status,
                KycPolicy = kyc,
                CountryCode = cc,
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
}
