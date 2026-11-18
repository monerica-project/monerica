using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>
    /// Pins how paid sponsors are placed on search results (unchanged by the 2026-11 quality
    /// ranking): sponsored results that match the search go to the top of their page.
    /// </summary>
    public class SearchSponsorOrderingTests
    {
        [Fact]
        public void MatchingSponsor_IsFirst_RegardlessOfOrganicRank()
        {
            // Organic order from the engine: a well-reviewed listing first, the sponsor last.
            var page = Vms(1, 2, 3);
            var sponsors = new[] { Sponsor(3, SponsorshipType.SubcategorySponsor, subCategoryId: 10) };

            var result = SearchSponsorOrdering.Apply(page, Entries(1, 2, 3), sponsors);

            Assert.Equal(new[] { 3, 1, 2 }, result.Select(v => v.DirectoryEntryId));
            Assert.True(result[0].IsSponsored);
            Assert.False(result[1].IsSponsored);
        }

        [Fact]
        public void SeveralSponsors_KeepSearchOrderAmongThemselves()
        {
            var page = Vms(1, 2, 3, 4);
            var sponsors = new[]
            {
                Sponsor(4, SponsorshipType.MainSponsor),
                Sponsor(2, SponsorshipType.CategorySponsor),
            };

            var result = SearchSponsorOrdering.Apply(page, Entries(1, 2, 3, 4), sponsors);

            Assert.Equal(new[] { 2, 4, 1, 3 }, result.Select(v => v.DirectoryEntryId));
        }

        [Fact]
        public void SponsorNotMatchingTheSearch_IsNotAdded()
        {
            var page = Vms(1, 2);
            var sponsors = new[] { Sponsor(99, SponsorshipType.MainSponsor) };

            var result = SearchSponsorOrdering.Apply(page, Entries(1, 2), sponsors);

            Assert.Equal(new[] { 1, 2 }, result.Select(v => v.DirectoryEntryId));
            Assert.DoesNotContain(result, v => v.IsSponsored);
        }

        [Fact]
        public void AdditionalSubcategoryResult_OnlySponsoredForThatSubcategoryOrMainSlot()
        {
            var page = Vms(1, 2);
            var entries = Entries(1, 2);
            entries[0].ShownViaAdditionalSubcategoryId = 500;
            entries[0].SubCategoryId = 20; // shown under its additional subcategory 20
            var sponsors = new[] { Sponsor(1, SponsorshipType.SubcategorySponsor, subCategoryId: 10) };

            var result = SearchSponsorOrdering.Apply(page, entries, sponsors);

            Assert.Equal(new[] { 1, 2 }, result.Select(v => v.DirectoryEntryId));
            Assert.False(result[0].IsSponsored);

            var main = SearchSponsorOrdering.Apply(Vms(1, 2), entries, new[] { Sponsor(1, SponsorshipType.MainSponsor) });
            Assert.True(main[0].IsSponsored);
        }

        private static List<DirectoryEntryViewModel> Vms(params int[] ids) =>
            ids.Select(id => new DirectoryEntryViewModel { DirectoryEntryId = id, Name = $"L{id}" }).ToList();

        private static List<DirectoryEntry> Entries(params int[] ids) =>
            ids.Select(id => new DirectoryEntry { DirectoryEntryId = id, Name = $"L{id}", DirectoryEntryKey = $"l{id}", SubCategoryId = 10 }).ToList();

        private static SponsoredListing Sponsor(int entryId, SponsorshipType type, int? subCategoryId = null) => new ()
        {
            DirectoryEntryId = entryId,
            SponsorshipType = type,
            SubCategoryId = subCategoryId,
            CampaignStartDate = DateTime.UtcNow.AddDays(-1),
            CampaignEndDate = DateTime.UtcNow.AddDays(1),
        };
    }
}
