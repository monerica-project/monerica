using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Tests.Helpers
{
    public class AdditionalSubcategoryTests
    {
        private static Subcategory Sub(int id, string name, string cat) => new ()
        {
            SubCategoryId = id,
            Name = name,
            SubCategoryKey = name.ToLowerInvariant().Replace(' ', '-'),
            Category = new Category { Name = cat, CategoryKey = cat.ToLowerInvariant() },
        };

        private static DirectoryEntry Trocador()
        {
            var e = new DirectoryEntry
            {
                DirectoryEntryId = 7,
                Name = "Trocador",
                DirectoryEntryKey = "trocador",
                Link = "https://trocador.app",
                LinkA = "https://trocador.app/?ref=main",
                Link2 = "http://trocadoronion.onion",
                Description = "Swap aggregator with minimal JavaScript.",
                DirectoryStatus = DirectoryStatus.Verified,
                SubCategoryId = 1,
                SubCategory = Sub(1, "Aggregators", "Exchanges"),
                EntryTags = new List<DirectoryEntryTag>(),
            };
            e.AdditionalSubcategories = new List<DirectoryEntryAdditionalSubcategory>
            {
                new ()
                {
                    DirectoryEntryAdditionalSubcategoryId = 100,
                    DirectoryEntryId = 7,
                    SubCategoryId = 2,
                    SubCategory = Sub(2, "Gift Cards", "Cards"),
                    Name = "Trocador Gift Cards",
                    Link = "https://trocador.app/en/giftcards",
                    Description = "Buy gift cards for Amazon and more with Monero.",
                },
            };
            return e;
        }

        [Fact]
        public void ApplyTo_UsesOverridesAndFallsBackForBlanks()
        {
            var main = Trocador();
            var shown = main.AdditionalSubcategories.First().ApplyTo(main);

            Assert.Equal("Trocador Gift Cards", shown.Name);
            Assert.Equal("https://trocador.app/en/giftcards", shown.Link);
            Assert.Equal("Buy gift cards for Amazon and more with Monero.", shown.Description);
            Assert.Equal("http://trocadoronion.onion", shown.Link2);          // blank override → main value
            Assert.Equal(2, shown.SubCategoryId);
            Assert.Equal("Gift Cards", shown.SubCategory!.Name);
            Assert.Equal(100, shown.ShownViaAdditionalSubcategoryId);
            Assert.Equal("trocador", shown.DirectoryEntryKey);                // same profile page
            Assert.Equal(DirectoryStatus.Verified, shown.DirectoryStatus);    // status is shared
        }

        [Fact]
        public void ApplyTo_OwnLinkDoesNotInheritMainAffiliateLink()
        {
            var main = Trocador();
            var shown = main.AdditionalSubcategories.First().ApplyTo(main);

            Assert.Null(shown.LinkA);
        }

        [Fact]
        public void ApplyTo_SharedLinkKeepsMainAffiliateLink()
        {
            var main = Trocador();
            var alt = main.AdditionalSubcategories.First();
            alt.Link = null;

            var shown = alt.ApplyTo(main);

            Assert.Equal("https://trocador.app", shown.Link);
            Assert.Equal("https://trocador.app/?ref=main", shown.LinkA);
        }

        [Fact]
        public void ApplyTo_DoesNotChangeTheMainListing()
        {
            var main = Trocador();
            main.AdditionalSubcategories.First().ApplyTo(main);

            Assert.Equal("Trocador", main.Name);
            Assert.Equal(1, main.SubCategoryId);
            Assert.Null(main.ShownViaAdditionalSubcategoryId);
        }

        [Fact]
        public void Search_ShowsAlternativeWording_WhenItMatchesBest()
        {
            var results = DirectorySearchEngine.Rank(new[] { Trocador() }, "gift cards");

            var only = Assert.Single(results);
            Assert.Equal("Trocador Gift Cards", only.Name);
        }

        [Fact]
        public void Search_FindsListing_ByDomainOfAnAdditionalLink()
        {
            var main = Trocador();
            main.SearchAdditionalLinks = new List<string> { "https://cupcakeswap.example/app" };

            var only = Assert.Single(DirectorySearchEngine.Rank(new[] { main }, "cupcakeswap"));

            Assert.Equal("Trocador", only.Name);
        }

        [Fact]
        public void Search_ShowsMainWording_WhenAlternativeRepeatsTheListingName()
        {
            var main = Trocador();
            var alt = main.AdditionalSubcategories.First();
            alt.Name = "Trocador Mining Pool";
            alt.Description = "Monero mining pool run by Trocador.";

            var only = Assert.Single(DirectorySearchEngine.Rank(new[] { main }, "trocador"));

            Assert.Equal("Trocador", only.Name);
            Assert.Null(only.ShownViaAdditionalSubcategoryId);
        }

        [Fact]
        public void Search_ShowsMainWording_ForMainMatches_AndOneResultPerListing()
        {
            var results = DirectorySearchEngine.Rank(new[] { Trocador() }, "trocador");

            var only = Assert.Single(results);
            Assert.Equal("Trocador", only.Name);
        }
    }
}
