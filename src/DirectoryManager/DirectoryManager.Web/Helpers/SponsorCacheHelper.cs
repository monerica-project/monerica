using DirectoryManager.Web.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Helpers
{
    public static class SponsorCacheHelper
    {
        // Drops every cached view of listings and sponsors, so a change (e.g. a newly paid
        // sponsorship) shows on the next request.
        public static void ClearSponsorCaches(IMemoryCache cache)
        {
            // Expire the token prefixes the filter's cached option lists are actually registered
            // under (categories, subcategories, tags, tag ids, countries).
            foreach (var prefix in StringConstants.FilterCacheTokenPrefixes)
            {
                CachePrefixManager.ExpirePrefix(prefix);
            }

            cache.Remove(StringConstants.CacheKeyAuthorPostCounts);
            cache.Remove(StringConstants.CacheKeyEntries);
            cache.Remove(StringConstants.CacheKeySponsoredListings);
            cache.Remove(StringConstants.CacheKeyAllActiveSponsors);
            cache.Remove(StringConstants.NavMenu);
            cache.Remove(StringConstants.CacheKeySponsorTickerItems);
        }
    }
}
