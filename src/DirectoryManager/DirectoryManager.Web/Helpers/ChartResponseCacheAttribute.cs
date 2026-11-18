using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Keeps a drawn chart image for a few minutes. The public price charts load every invoice and
    /// draw an image on each request; without this, a loop of requests from anyone costs a full
    /// database read and a drawing each time. The cache is small and bounded, so asking for many
    /// different (made-up) placements cannot grow it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ChartResponseCacheAttribute : Attribute, IAsyncActionFilter
    {
        private const int MaxKeyLength = 300;
        private const int MaxImageBytes = 2_000_000;
        private static readonly MemoryCache Cache = new (new MemoryCacheOptions { SizeLimit = 400 });

        /// <summary>How long one drawn image is reused.</summary>
        public int Seconds { get; set; } = 300;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // The key is built from the values the action actually bound (enums and numbers),
            // not from the raw query string, so extra parameters cannot create new entries.
            var key = context.ActionDescriptor.DisplayName + "|" + string.Join(
                "&", context.ActionArguments.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => a.Key + "=" + a.Value));
            var usable = key.Length <= MaxKeyLength;

            if (usable && Cache.TryGetValue(key, out CachedImage? hit) && hit != null)
            {
                context.Result = new FileContentResult(hit.Bytes, hit.ContentType);
                return;
            }

            var executed = await next();
            if (usable && executed.Result is FileContentResult file && file.FileContents.Length <= MaxImageBytes)
            {
                Cache.Set(
                    key,
                    new CachedImage(file.FileContents, file.ContentType),
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(this.Seconds), Size = 1 });
            }
        }

        private sealed record CachedImage(byte[] Bytes, string ContentType);
    }
}
