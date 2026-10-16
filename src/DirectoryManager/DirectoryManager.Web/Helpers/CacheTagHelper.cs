using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.TagHelpers
{
    [HtmlTargetElement("cache-content")]
    public class CacheContentTagHelper : TagHelper
    {
        private const double MinimumCacheSeconds = 1;

        private readonly IMemoryCache cache;
        private readonly ISponsoredListingRepository sponsoredListingRepository;

        public CacheContentTagHelper(
            IMemoryCache cache,
            ISponsoredListingRepository sponsoredListingRepository)
        {
            this.cache = cache;
            this.sponsoredListingRepository = sponsoredListingRepository;
        }

        [HtmlAttributeName("cache-key")]
        public string CacheKey { get; set; } = StringConstants.CacheKeyEntries;

        [HtmlAttributeName("cache-duration-seconds")]
        public int CacheDurationSeconds { get; set; } = IntegerConstants.CacheDurationSeconds;

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; } = null!;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!this.cache.TryGetValue(this.CacheKey, out string? cachedContent))
            {
                var nextExpirationDate = await this.sponsoredListingRepository.GetNextExpirationDateAsync();
                var childContent = await output.GetChildContentAsync();
                cachedContent = childContent.GetContent();

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ComputeExpiration(
                        nextExpirationDate, DateTime.UtcNow, this.CacheDurationSeconds)
                };

                this.cache.Set(this.CacheKey, cachedContent, cacheEntryOptions);
            }

            output.Content.SetHtmlContent(cachedContent);
            output.TagName = null;
        }

        /// <summary>
        /// Cache lifetime: the configured duration, shortened so it ends when the next sponsorship
        /// expires. Never zero/negative (a sponsorship that expires "now" or a clock skew would
        /// otherwise make MemoryCache throw ArgumentOutOfRangeException) — at least 1 second.
        /// </summary>
        internal static TimeSpan ComputeExpiration(DateTime? nextExpirationDate, DateTime utcNow, int durationSeconds)
        {
            double seconds = Math.Max(1, durationSeconds);

            if (nextExpirationDate.HasValue && nextExpirationDate.Value != DateTime.MinValue)
            {
                seconds = Math.Min((nextExpirationDate.Value - utcNow).TotalSeconds, seconds);
            }

            return TimeSpan.FromSeconds(Math.Max(MinimumCacheSeconds, seconds));
        }
    }
}