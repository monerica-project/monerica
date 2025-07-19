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

            // e.g. your snippet for app.monerica.com (no trailing slash)
            this.appDomain = this.cache
                          .GetSnippet(SiteConfigSetting.AppDomain)
                          ?.TrimEnd('/')
                        ?? string.Empty;

            this.canonicalDomain = this.cache.GetSnippet(SiteConfigSetting.CanonicalDomain);
        }

        public bool IsTor
        {
            get
            {
                var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;
                return host.EndsWith(StringConstants.TorDomain, StringComparison.OrdinalIgnoreCase);
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

                var cd = this.canonicalDomain ?? string.Empty;
                return cd.TrimEnd('/');
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

            // If they passed an absolute URL, just return it
            if (Uri.TryCreate(path, UriKind.Absolute, out _))
            {
                return path;
            }

            // Normalize to leading slash
            if (!path.StartsWith("/"))
            {
                path = string.Concat("/", path);
            }

            var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;

            // If on Tor, keep it relative
            if (this.IsTor || this.IsLocal)
            {
                return path;
            }

            // Otherwise send to the app subdomain
            return $"{this.appDomain}{path}";
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

            // If it's already an absolute URL, just return it
            if (Uri.TryCreate(path, UriKind.Absolute, out _))
            {
                return path;
            }

            // Normalize incoming path: strip all leading/trailing slashes, then add exactly one leading slash
            path = string.Concat("/", path.Trim('/'));

            var host = this.http.HttpContext?.Request.Host.Host ?? string.Empty;

            // On Tor or local, stay relative
            if (this.IsTor || this.IsLocal)
            {
                return path;
            }

            // Otherwise combine with canonicalDomain, trimming any trailing slash there
            var cd = this.canonicalDomain?.TrimEnd('/') ?? string.Empty;
            return string.Concat(cd, path);
        }
    }
}