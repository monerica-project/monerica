using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DirectoryManager.Web.Services.Implementations
{
    public class UrlResolutionService : IUrlResolutionService
    {
        private readonly IHttpContextAccessor http;
        private readonly ICacheService cache;
        private readonly IWebHostEnvironment env;
        private readonly ILogger<UrlResolutionService> logger;
        private readonly string torSuffix;

        public UrlResolutionService(
            IHttpContextAccessor httpContextAccessor,
            ICacheService cacheService,
            IWebHostEnvironment env,
            ILogger<UrlResolutionService> logger)
        {
            this.http = httpContextAccessor;
            this.cache = cacheService;
            this.env = env;
            this.logger = logger;
            this.torSuffix = StringConstants.TorDomain;
        }

        private string AppDomain => this.GetSetting(SiteConfigSetting.AppDomain);
        private string CanonicalDomain => this.GetSetting(SiteConfigSetting.CanonicalDomain);

        private string GetSetting(SiteConfigSetting key)
        {
            try
            {
                var v = this.cache.GetSnippetAsync(key).GetAwaiter().GetResult();
                return (v ?? string.Empty).TrimEnd('/');
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "[UrlResolver] failed to read setting {Key}", key);
                return string.Empty;
            }
        }

        public bool IsTor
        {
            get
            {
                var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;
                return host.EndsWith(this.torSuffix, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsLocal
        {
            get
            {
                if (!this.env.IsDevelopment())
                {
                    return false;
                }

                var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;
                return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || host.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string BaseUrl => (this.IsTor || this.IsLocal) ? string.Empty : this.CanonicalDomain;

        public string ResolveToApp(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            // FIX: only treat as absolute if scheme is http/https.
            // On Linux, "/foo/bar" parses as a valid absolute file:// URI.
            if (Uri.TryCreate(path, UriKind.Absolute, out var abs) &&
                (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
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

            if (string.IsNullOrEmpty(this.AppDomain))
            {
                return path;
            }

            return $"{this.AppDomain}{path}";
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

            // FIX: same scheme guard.
            if (Uri.TryCreate(path, UriKind.Absolute, out var abs) &&
                (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            {
                return path;
            }

            path = "/" + path.Trim('/');

            if (this.IsTor || this.IsLocal)
            {
                return path;
            }

            return string.IsNullOrEmpty(this.CanonicalDomain) ? path : $"{this.CanonicalDomain}{path}";
        }

        public string ExtractPathFromFullUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            // FIX: same scheme guard.
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return "/" + url.TrimStart('/');
            }

            var domains = new[]
            {
        this.AppDomain,
        this.CanonicalDomain
            }
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d.Replace("https://", string.Empty)
                          .Replace("http://", string.Empty)
                          .TrimEnd('/'))
            .ToList();

            if (domains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.AbsolutePath;
            }

            return url;
        }
    }
}