using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Models.StaticPublishing;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Which mirrored pages a change touches. Pure: the service loads the facts, this decides the
    /// paths. Paths are built exactly like <see cref="SiteMapController"/> builds sitemap URLs
    /// (same keys, same page sizes), so they match the files the full crawl writes. Paged lists
    /// get one extra page beyond the current count, so a page that just disappeared (listing moved
    /// out) is re-checked too — the publisher then removes it from the mirror.
    /// </summary>
    public static class StaticPublishPathMap
    {
        /// <summary>Pages that show something about every listing (newest, trust pages, feeds, data files).</summary>
        public static readonly IReadOnlyList<string> SiteWideListingPages = new[]
        {
            "/",
            "/newest",
            "/" + PublicReviewsController.PagePath,
            "/" + IncidentsController.PagePath,
            "/" + SponsorsAndFeaturedController.PagePath,
            "/sitemap.xml",
            "/rss/feed.xml",
            "/llms.txt",
            "/llms-full.txt",
            "/data.json",
            "/feed.json",
        };

        /// <summary>Every page a listing appears on: its profile (all review pages), each subcategory
        /// and category it is listed in (all pages), its tags, its country, the indexes and the
        /// site-wide pages. Used for listing edits AND review/reply changes (ratings show in lists).</summary>
        public static List<string> ForListing(ListingPageFacts facts)
        {
            var paths = new List<string>();

            if (!string.IsNullOrWhiteSpace(facts.EntryKey))
            {
                var listing = UrlBuilder.ListingPath(facts.EntryKey);
                paths.Add(listing);
                AddPages(paths, listing, facts.CrowdReviewCount, IntegerConstants.ReviewsPageSize);
            }

            foreach (var p in facts.Placements.Where(p => !string.IsNullOrWhiteSpace(p.CategoryKey) && !string.IsNullOrWhiteSpace(p.SubcategoryKey)))
            {
                var sub = $"/{p.CategoryKey}/{p.SubcategoryKey}";
                paths.Add(sub);
                AddPages(paths, sub, p.SubcategoryEntryCount, IntegerConstants.MediumPageSize);
            }

            foreach (var p in facts.Placements.Where(p => !string.IsNullOrWhiteSpace(p.CategoryKey)))
            {
                var cat = "/" + p.CategoryKey;
                paths.Add(cat);
                AddPages(paths, cat, p.CategoryEntryCount, IntegerConstants.MediumPageSize);
            }

            paths.AddRange(SiteWideListingPages);

            foreach (var t in facts.Tags.Where(t => !string.IsNullOrWhiteSpace(t.Slug)))
            {
                var tag = "/tagged/" + t.Slug;
                paths.Add(tag);
                AddPages(paths, tag, t.EntryCount, IntegerConstants.MaxPageSize);
            }

            if (facts.Tags.Count > 0)
            {
                paths.Add("/tagged");
                AddPages(paths, "/tagged", facts.TagCount, IntegerConstants.MaxPageSize);
            }

            if (facts.Country != null && !string.IsNullOrWhiteSpace(facts.Country.Slug))
            {
                var country = "/countries/" + facts.Country.Slug;
                paths.Add(country);
                AddPages(paths, country, facts.Country.EntryCount, IntegerConstants.MaxPageSize);
                paths.Add("/countries");
                AddPages(paths, "/countries", facts.CountryCount, IntegerConstants.MaxPageSize);
            }

            return Distinct(paths);
        }

        /// <summary>Union of the pages before and after a change (a moved listing leaves pages too).</summary>
        public static List<string> Union(IEnumerable<string>? before, IEnumerable<string> after) =>
            Distinct((before ?? Enumerable.Empty<string>()).Concat(after));

        /// <summary>"/x/page/2" .. one page past the last page that <paramref name="count"/> items fill.</summary>
        public static void AddPages(List<string> paths, string basePath, int count, int pageSize)
        {
            if (pageSize <= 0)
            {
                return;
            }

            var lastWithSlack = (int)Math.Ceiling((Math.Max(0, count) + 1) / (double)pageSize);
            for (var page = 2; page <= lastWithSlack; page++)
            {
                paths.Add($"{basePath}/page/{page}");
            }
        }

        private static List<string> Distinct(IEnumerable<string> paths)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<string>();
            foreach (var p in paths)
            {
                if (seen.Add(p))
                {
                    list.Add(p);
                }
            }

            return list;
        }
    }
}
