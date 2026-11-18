using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// How paid sponsors are placed on a search results page (unchanged from before the 2026-11
    /// ranking work, only moved here so it can be tested): on each page, results whose listing
    /// has any active sponsorship are marked sponsored and moved to the top of that page, keeping
    /// the search order within the sponsored and the unsponsored groups. A result shown under one
    /// of the listing's ADDITIONAL subcategories only counts as sponsored if the listing holds the
    /// main sponsor slot or a subcategory sponsorship for that exact subcategory. Sponsors that
    /// don't match the search are never added.
    /// </summary>
    public static class SearchSponsorOrdering
    {
        public static List<DirectoryEntryViewModel> Apply(
            IList<DirectoryEntryViewModel> page,
            IEnumerable<DirectoryEntry> pageEntries,
            IEnumerable<SponsoredListing> activeSponsors)
        {
            var allSponsors = activeSponsors.ToList();
            var sponsorIds = new HashSet<int>(allSponsors.Select(s => s.DirectoryEntryId));

            var shownViaAlternative = pageEntries
                .Where(e => e.ShownViaAdditionalSubcategoryId.HasValue)
                .ToDictionary(e => e.DirectoryEntryId, e => e.SubCategoryId);

            foreach (var item in page)
            {
                if (shownViaAlternative.TryGetValue(item.DirectoryEntryId, out var altSubId))
                {
                    item.IsSponsored = allSponsors.Any(s => s.DirectoryEntryId == item.DirectoryEntryId &&
                        (s.SponsorshipType == SponsorshipType.MainSponsor ||
                         (s.SponsorshipType == SponsorshipType.SubcategorySponsor && s.SubCategoryId == altSubId)));
                }
                else
                {
                    item.IsSponsored = sponsorIds.Contains(item.DirectoryEntryId);
                }
            }

            return page.Where(e => e.IsSponsored).Concat(page.Where(e => !e.IsSponsored)).ToList();
        }
    }
}
