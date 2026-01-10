using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public class UrlResolutionService : IUrlResolutionService
    {
        private readonly IHttpContextAccessor http;
        private readonly ICacheService cache;
        private readonly string torSuffix;
        private readonly string appDomain;
        private readonly string canonicalDomain;

        public UrlResolutionService(
            IHttpContextAccessor httpContextAccessor,
            ICacheService cacheService)
        {
            this.http = httpContextAccessor;
            this.cache = cacheService;
            this.torSuffix = StringConstants.TorDomain;

            // Block at the constructor boundary (DI factories are sync)
            var app = this.cache.GetSnippetAsync(SiteConfigSetting.AppDomain)
                           .GetAwaiter().GetResult();
            this.appDomain = (app ?? string.Empty).TrimEnd('/');

            var cd = this.cache.GetSnippetAsync(SiteConfigSetting.CanonicalDomain)
                          .GetAwaiter().GetResult();
            this.canonicalDomain = (cd ?? string.Empty).TrimEnd('/');
        }

        public bool IsTor
        {
            get
            {
                var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;
                return host.EndsWith(this.torSuffix, StringComparison.OrdinalIgnoreCase);
            }
        }

        public string BaseUrl
        {
            get
            {
                if (this.IsTor || this.IsLocal)
                {
                    return string.Empty;
                }

                return this.canonicalDomain; // already trimmed in ctor
            }
        }

        private bool IsLocal
        {
            get
            {
                var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;
                return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || host.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string ResolveToApp(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out _))
            {
                return path;
            }

            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            if (this.IsTor || this.IsLocal)
            {
                return path;
            }

            // If appDomain is empty (missing snippet), fall back to relative
            return string.IsNullOrEmpty(this.appDomain) ? path : $"{this.appDomain}{path}";
        }

        public string ResolveToRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (path == "~/")
            {
                path = "/";
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out _))
            {
                return path;
            }

            path = "/" + path.Trim('/');

            if (this.IsTor || this.IsLocal)
            {
                return path;
            }

            // If canonicalDomain is empty, fall back to relative
            return string.IsNullOrEmpty(this.canonicalDomain) ? path : $"{this.canonicalDomain}{path}";
        }

        public string ExtractPathFromFullUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                // Not an absolute URL — treat it as a relative path and normalize
                return "/" + url.TrimStart('/');
            }

            // Normalize domains (ctor already trimmed)
            var domains = new[]
            {
                this.appDomain,
                this.canonicalDomain
            }
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d.Replace("https://", "")
                         .Replace("http://", "")
                         .TrimEnd('/'))
            .ToList();

            var host = uri.Host;

            // If URL matches one of the known domains → return only the path
            if (domains.Any(d => host.Equals(d, StringComparison.OrdinalIgnoreCase)))
            {
                // uri.AbsolutePath ALWAYS starts with "/"
                return uri.AbsolutePath;
            }

            // If it's some other domain → return original URL (or choose different behavior)
            return url;
        }
    }
}
