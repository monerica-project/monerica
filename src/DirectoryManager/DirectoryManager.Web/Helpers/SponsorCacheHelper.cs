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
            CachePrefixManager.ExpirePrefix(StringConstants.DirectoryFilterCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.ActiveSubcategoriesByCategoryCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.ActiveTagsByCategoryCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.ActiveTagIdsByCategoryCachePrefix);
            CachePrefixManager.ExpirePrefix(StringConstants.CacheKeyPrefixConversion);
            CachePrefixManager.ExpirePrefix(StringConstants.PrefixActiveTagsByCat);
            cache.Remove(StringConstants.CacheKeyAuthorPostCounts);
            cache.Remove(StringConstants.CacheKeyEntries);
            cache.Remove(StringConstants.CacheKeySponsoredListings);
            cache.Remove(StringConstants.CacheKeyAllActiveSponsors);
            cache.Remove(StringConstants.NavMenu);
            cache.Remove(StringConstants.CacheKeySponsorTickerItems);
        }
    }
}
