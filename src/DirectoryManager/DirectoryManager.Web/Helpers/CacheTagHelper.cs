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
        public string CacheKey { get; set; } = StringConstants.EntriesCacheKey;

        [HtmlAttributeName("cache-duration-seconds")]
        public int CacheDurationSeconds { get; set; } = IntegerConstants.CacheDurationSeconds;

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; } = null!;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!this.cache.TryGetValue(this.CacheKey, out string? cachedContent))
            {
                var nextExpirationDate = await this.sponsoredListingRepository.GetNextExpirationDate();
                var childContent = await output.GetChildContentAsync();
                cachedContent = childContent.GetContent();

                // Determine the cache expiration time
                var cacheExpiration = (nextExpirationDate.HasValue && nextExpirationDate != DateTime.MinValue)
                    ? TimeSpan.FromSeconds(Math.Min((nextExpirationDate.Value - DateTime.UtcNow).TotalSeconds, this.CacheDurationSeconds))
                    : TimeSpan.FromSeconds(this.CacheDurationSeconds);

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheExpiration
                };

                this.cache.Set(this.CacheKey, cachedContent, cacheEntryOptions);
            }

            output.Content.SetHtmlContent(cachedContent);
            output.TagName = null;
        }
    }
}