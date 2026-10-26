using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    // Per-placement sponsorship pricing: the main slot, each category page and each subcategory
    // page has its own price list, falling back to the standard price list for its type.
    public class SponsoredListingOfferPlacementTests
    {
        private const int Exchanges = 8;
        private const int Wallets = 18;
        private const int InstantSwaps = 73;
        private const int P2P = 77;
        private const int MobileWallets = 117;

        // ---------------- price lookup ----------------
        [Fact]
        public async Task Main_UsesStandardPrice()
        {
            using var context = Seeded();
            var repo = new SponsoredListingOfferRepository(context);

            var offers = (await repo.GetOffersForPlacementAsync(SponsorshipType.MainSponsor, null)).ToList();

            Assert.Equal(new[] { 2664m }, offers.Select(o => o.Price));
        }

        [Fact]
        public async Task Category_WithOwnPrice_UsesIt()
        {
            using var context = Seeded();
            var repo = new SponsoredListingOfferRepository(context);

            var offers = (await repo.GetOffersForPlacementAsync(SponsorshipType.CategorySponsor, Exchanges)).ToList();

            Assert.Equal(new[] { 600m, 1140m }, offers.Select(o => o.Price));
        }

        [Fact]
        public async Task Category_WithoutOwnPrice_UsesStandardPrice()
        {
            using var context = Seeded();
            var repo = new SponsoredListingOfferRepository(context);

            var offers = (await repo.GetOffersForPlacementAsync(SponsorshipType.CategorySponsor, Wallets)).ToList();

            Assert.Equal(new[] { 100m, 190m }, offers.Select(o => o.Price));
        }

        [Fact]
        public async Task Category_WithOnlyDisabledOwnPrices_UsesStandardPrice()
        {
            using var context = Seeded();
            foreach (var o in context.SponsoredListingOffers.Where(o => o.CategoryId == Exchanges))
            {
                o.IsEnabled = false;
            }

            await context.SaveChangesAsync();
            var repo = new SponsoredListingOfferRepository(context);

            var offers = (await repo.GetOffersForPlacementAsync(SponsorshipType.CategorySponsor, Exchanges)).ToList();

            Assert.Equal(new[] { 100m, 190m }, offers.Select(o => o.Price));
        }

        [Fact]
        public async Task Subcategory_WithOwnPrice_UsesIt_OthersUseStandard()
        {
            using var context = Seeded();
            var repo = new SponsoredListingOfferRepository(context);

            var own = (await repo.GetOffersForPlacementAsync(SponsorshipType.SubcategorySponsor, InstantSwaps)).ToList();
            var other = (await repo.GetOffersForPlacementAsync(SponsorshipType.SubcategorySponsor, P2P)).ToList();

            Assert.Equal(new[] { 850.5m }, own.Select(o => o.Price));
            Assert.Equal(new[] { 70m }, other.Select(o => o.Price));
        }

        [Fact]
        public async Task CategoryPrice_IsNotUsedForASubcategoryWithTheSameId()
        {
            // Category 8 has its own price; a subcategory with id 8 must not pick it up.
            using var context = Seeded();
            var repo = new SponsoredListingOfferRepository(context);

            var offers = (await repo.GetOffersForPlacementAsync(SponsorshipType.SubcategorySponsor, Exchanges)).ToList();

            Assert.Equal(new[] { 70m }, offers.Select(o => o.Price));
        }

        [Fact]
        public async Task CategoryPrice_IsTheSameForEveryListingInTheCategory()
        {
            // The old model priced the category page by the BUYER's subcategory; the page now has one price.
            using var context = Seeded();
            var repo = new SponsoredListingOfferRepository(context);

            var a = await repo.GetOffersForPlacementAsync(SponsorshipType.CategorySponsor, Exchanges);
            var b = await repo.GetOffersForPlacementAsync(SponsorshipType.CategorySponsor, Exchanges);

            Assert.Equal(a.Select(o => o.SponsoredListingOfferId), b.Select(o => o.SponsoredListingOfferId));
        }

        // ---------------- placement rules ----------------
        [Theory]
        [InlineData(SponsorshipType.MainSponsor, null, null, true)]
        [InlineData(SponsorshipType.MainSponsor, 8, null, false)]
        [InlineData(SponsorshipType.MainSponsor, null, 73, false)]
        [InlineData(SponsorshipType.CategorySponsor, null, null, true)]
        [InlineData(SponsorshipType.CategorySponsor, 8, null, true)]
        [InlineData(SponsorshipType.CategorySponsor, null, 73, false)]
        [InlineData(SponsorshipType.SubcategorySponsor, null, null, true)]
        [InlineData(SponsorshipType.SubcategorySponsor, null, 73, true)]
        [InlineData(SponsorshipType.SubcategorySponsor, 8, null, false)]
        [InlineData(SponsorshipType.Unknown, null, null, false)]
        public void Normalize_EnforcesOnePlacementPerType(SponsorshipType type, int? categoryId, int? subcategoryId, bool valid)
        {
            var offer = Offer(1, type, 30, 1m, categoryId, subcategoryId);

            Assert.Equal(valid, SponsoredListingOfferPlacement.Normalize(offer) == null);
        }

        [Fact]
        public void IsLivePlacement_HidesRetiredPages()
        {
            var enabledCat = new Category { CategoryId = 1, Name = "On", CategoryKey = "on", IsEnabled = true };
            var disabledCat = new Category { CategoryId = 2, Name = "Off", CategoryKey = "off", IsEnabled = false };
            var liveSub = new Subcategory { SubCategoryId = 10, Name = "Live", SubCategoryKey = "live", IsEnabled = true, Category = enabledCat };
            var offSub = new Subcategory { SubCategoryId = 11, Name = "Off", SubCategoryKey = "off", IsEnabled = false, Category = enabledCat };
            var noSponsorSub = new Subcategory { SubCategoryId = 12, Name = "NoAds", SubCategoryKey = "no-ads", IsEnabled = true, SponsorshipDisabled = true, Category = enabledCat };

            Assert.True(SponsoredListingOfferPlacement.IsLivePlacement(Offer(1, SponsorshipType.CategorySponsor, 30, 1m, null, null)));
            Assert.True(SponsoredListingOfferPlacement.IsLivePlacement(WithCategory(Offer(2, SponsorshipType.CategorySponsor, 30, 1m, 1, null), enabledCat)));
            Assert.False(SponsoredListingOfferPlacement.IsLivePlacement(WithCategory(Offer(3, SponsorshipType.CategorySponsor, 30, 1m, 2, null), disabledCat)));
            Assert.True(SponsoredListingOfferPlacement.IsLivePlacement(WithSub(Offer(4, SponsorshipType.SubcategorySponsor, 30, 1m, null, 10), liveSub)));
            Assert.False(SponsoredListingOfferPlacement.IsLivePlacement(WithSub(Offer(5, SponsorshipType.SubcategorySponsor, 30, 1m, null, 11), offSub)));
            Assert.False(SponsoredListingOfferPlacement.IsLivePlacement(WithSub(Offer(6, SponsorshipType.SubcategorySponsor, 30, 1m, null, 12), noSponsorSub)));
        }

        [Fact]
        public void Label_NamesThePlacement()
        {
            var cat = new Category { CategoryId = 8, Name = "Exchanges", CategoryKey = "exchanges" };
            var sub = new Subcategory { SubCategoryId = 73, Name = "Instant Swaps", SubCategoryKey = "instant-swaps", Category = cat };

            Assert.Equal(SponsoredListingOfferPlacement.StandardLabel, SponsoredListingOfferPlacement.Label(Offer(1, SponsorshipType.CategorySponsor, 30, 1m, null, null)));
            Assert.Equal("Exchanges", SponsoredListingOfferPlacement.Label(WithCategory(Offer(2, SponsorshipType.CategorySponsor, 30, 1m, 8, null), cat)));
            Assert.Equal("Exchanges > Instant Swaps", SponsoredListingOfferPlacement.Label(WithSub(Offer(3, SponsorshipType.SubcategorySponsor, 30, 1m, null, 73), sub)));
        }

        private static SponsoredListingOffer WithCategory(SponsoredListingOffer o, Category c)
        {
            o.Category = c;
            return o;
        }

        private static SponsoredListingOffer WithSub(SponsoredListingOffer o, Subcategory s)
        {
            o.Subcategory = s;
            return o;
        }

        private static SponsoredListingOffer Offer(int id, SponsorshipType type, int days, decimal price, int? categoryId, int? subcategoryId, bool enabled = true) =>
            new SponsoredListingOffer
            {
                SponsoredListingOfferId = id,
                SponsorshipType = type,
                Days = days,
                Price = price,
                PriceCurrency = Currency.USD,
                CategoryId = categoryId,
                SubcategoryId = subcategoryId,
                IsEnabled = enabled,
                Description = $"{days} days",
                CreatedByUserId = "test",
            };

        private static ApplicationDbContext Seeded()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"offers-{Guid.NewGuid():N}")
                .Options;
            var context = new ApplicationDbContext(options);

            context.SponsoredListingOffers.AddRange(
                Offer(1, SponsorshipType.MainSponsor, 30, 2664m, null, null),
                Offer(2, SponsorshipType.CategorySponsor, 30, 100m, null, null),
                Offer(3, SponsorshipType.CategorySponsor, 60, 190m, null, null),
                Offer(4, SponsorshipType.CategorySponsor, 30, 600m, Exchanges, null),
                Offer(5, SponsorshipType.CategorySponsor, 60, 1140m, Exchanges, null),
                Offer(6, SponsorshipType.SubcategorySponsor, 30, 70m, null, null),
                Offer(7, SponsorshipType.SubcategorySponsor, 30, 850.5m, null, InstantSwaps),
                Offer(8, SponsorshipType.SubcategorySponsor, 30, 999m, null, MobileWallets, enabled: false));
            context.SaveChanges();
            return context;
        }
    }
}
