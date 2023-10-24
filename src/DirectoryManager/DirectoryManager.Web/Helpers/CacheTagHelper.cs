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

        public CacheContentTagHelper(IMemoryCache cache)
        {
            this.cache = cache;
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
                var childContent = await output.GetChildContentAsync();
                cachedContent = childContent.GetContent();

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(this.CacheDurationSeconds)
                };

                this.cache.Set(this.CacheKey, cachedContent, cacheEntryOptions);
            }

            output.Content.SetHtmlContent(cachedContent);
            output.TagName = null;
        }
    }
}
