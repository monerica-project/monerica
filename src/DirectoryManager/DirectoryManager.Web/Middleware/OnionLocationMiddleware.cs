using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Middleware
{
    // Mirrors the <meta http-equiv="onion-location"> tag emitted in _BaseLayout.cshtml
    // as an HTTP "Onion-Location" header so browsers (e.g. Tor Browser) can read it
    // without parsing the document body. Same source of truth: the cached
    // SiteConfigSetting.FullRootTorUrl snippet + the current path/query.
    public class OnionLocationMiddleware
    {
        private const string OnionLocationHeader = "Onion-Location";

        private readonly RequestDelegate next;

        public OnionLocationMiddleware(RequestDelegate next) => this.next = next;

        // ICacheService is scoped, so it is injected per-request via InvokeAsync.
        public async Task InvokeAsync(HttpContext context, ICacheService cacheService)
        {
            await this.AddOnionLocationHeaderAsync(context, cacheService);
            await this.next(context);
        }

        private async Task AddOnionLocationHeaderAsync(HttpContext context, ICacheService cacheService)
        {
            // Onion-Location only applies to navigable document loads.
            if (!(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
            {
                return;
            }

            // No point advertising the onion address when already served over the onion host.
            if (context.Request.Host.Host.EndsWith(StringConstants.TorDomain, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var torUrl = await cacheService.GetSnippetAsync(SiteConfigSetting.FullRootTorUrl);
            if (string.IsNullOrWhiteSpace(torUrl))
            {
                return;
            }

            torUrl = UrlHelper.NormalizeUrl(torUrl);
            var currentPath = context.Request.Path + context.Request.QueryString;

            context.Response.Headers[OnionLocationHeader] = $"{torUrl}{currentPath}";
        }
    }
}