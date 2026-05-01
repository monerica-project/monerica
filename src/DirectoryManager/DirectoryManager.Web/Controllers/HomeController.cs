using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly IRssFeedService rssFeedService;
        private readonly IMemoryCache cache;
        private readonly ICacheService cacheService;
        private readonly ISponsoredListingRepository sponsoredListingRepository;

        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly IDirectoryEntryReviewCommentRepository commentRepository;
        private readonly IUrlResolutionService urlResolver;
        private readonly IWebHostEnvironment env;

        public HomeController(
            IDirectoryEntryRepository directoryEntryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IRssFeedService rssFeedService,
            IMemoryCache cache,
            ICacheService cacheService,
            ISponsoredListingRepository sponsoredListingRepository,
            IDirectoryEntryReviewRepository reviewRepository,
            IDirectoryEntryReviewCommentRepository commentRepository,
            IUrlResolutionService urlResolver,
            IWebHostEnvironment env)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.rssFeedService = rssFeedService;
            this.cache = cache;
            this.cacheService = cacheService;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.reviewRepository = reviewRepository;
            this.commentRepository = commentRepository;
            this.urlResolver = urlResolver;
            this.env = env;
        }

        [HttpGet("/")]
        public async Task<IActionResult> IndexAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, string.Empty);

            // ✅ Load homepage “Latest Reviews” + “Latest Comments”
            var latestReviews = await this.reviewRepository.ListLatestApprovedAsync(IntegerConstants.ReviewCountToShowOnHomepage);

            // NOTE: method name may differ in your repo — see note below
            var latestComments = await this.commentRepository.ListLatestApprovedAsync(IntegerConstants.CommentCountToShowOnHomepage);

            this.ViewBag.LatestReviews = latestReviews;
            this.ViewBag.LatestComments = latestComments;

            return this.View();
        }

        [HttpGet("contact")]
        public async Task<IActionResult> ContactAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "contact");
            return this.View();
        }

        [HttpGet("faq")]
        public async Task<IActionResult> FAQAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "faq");
            return this.View();
        }

        [HttpGet("donate")]
        public async Task<IActionResult> DonateAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "donate");
            return this.View();
        }

        [HttpGet("about")]
        public async Task<IActionResult> AboutAsync()
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            this.ViewData[StringConstants.CanonicalUrl] = UrlBuilder.CombineUrl(canonicalDomain, "about");
            return this.View();
        }

        [HttpGet("pgp")]
        public async Task<IActionResult> PgpAsync()
        {
            var pgpKey = await this.cacheService.GetSnippetAsync(SiteConfigSetting.PgpKey);
            return this.Content(pgpKey);
        }

        [HttpGet("newest")]
        [HttpGet("newest/page/{pageNumber:int}")]
        public async Task<IActionResult> Newest(int pageNumber = 1, int pageSize = IntegerConstants.MaxPageSize)
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            var basePath = "newest";
            var path = pageNumber > 1
                ? $"{basePath}/page/{pageNumber}"
                : basePath;
            this.ViewData[StringConstants.CanonicalUrl] =
                UrlBuilder.CombineUrl(canonicalDomain, path);

            var groupedNewestAdditions = await this.directoryEntryRepository.GetNewestAdditionsGrouped(pageSize, pageNumber);

            int totalEntries = await this.directoryEntryRepository.TotalActive();
            this.ViewBag.TotalEntries = totalEntries;
            this.ViewBag.TotalPages = (int)Math.Ceiling((double)totalEntries / pageSize);
            this.ViewBag.PageNumber = pageNumber;

            return this.View("Newest", groupedNewestAdditions);
        }

        [Authorize]
        [HttpGet("expire-cache")]
        public IActionResult ExpireCache()
        {
            this.ExpireCache();

            return this.View();
        }

        [HttpGet("/snippets/main-sponsors")]
        [Produces("text/html")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        [AllowAnonymous]
        public async Task<IActionResult> MainSponsorsAsync()
        {
            // If you need CORS for fetch():
            this.Response.Headers["Access-Control-Allow-Origin"] = "*";

            var sponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            return this.PartialView("_MainSponsorsSnippet", sponsors);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 🔧 DEBUG: GET /debug/url-resolver
        // Dumps everything the UrlResolutionService can see so you can tell
        // exactly which branch (IsTor / IsLocal / empty AppDomain) is firing.
        //
        // ⚠️ Remove or re-add [Authorize] before production. For now it's open
        //    so you can hit it from a fresh browser without dealing with auth.
        // ─────────────────────────────────────────────────────────────────────
        [Authorize]
        [HttpGet("/debug/url-resolver")]
        public async Task<IActionResult> DebugUrlResolverAsync()
        {
            // Raw cache values (BEFORE any TrimEnd / processing)
            string? rawAppDomain = null;
            string? rawCanonicalDomain = null;
            string? rawAppErr = null;
            string? rawCanonicalErr = null;

            try
            {
                rawAppDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.AppDomain);
            }
            catch (Exception ex)
            {
                rawAppErr = ex.ToString();
            }

            try
            {
                rawCanonicalDomain = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain);
            }
            catch (Exception ex)
            {
                rawCanonicalErr = ex.ToString();
            }

            var req = this.HttpContext.Request;

            // Probe paths — same ones your forms/views actually use
            var probePaths = new[]
            {
                "/directory-entry-reviews/begin",
                "/directory-entry-review-replies/begin",
                "/site/some-entry-key",
                "newest",
                "~/",
                "https://already-absolute.example.com/x"
            };

            var resolveToApp = probePaths.ToDictionary(
                p => p,
                p => SafeCall(() => this.urlResolver.ResolveToApp(p)));

            var resolveToRoot = probePaths.ToDictionary(
                p => p,
                p => SafeCall(() => this.urlResolver.ResolveToRoot(p)));

            var headerSnapshot = new Dictionary<string, string?>
            {
                ["Host"] = req.Headers["Host"].ToString(),
                ["X-Forwarded-Host"] = req.Headers["X-Forwarded-Host"].ToString(),
                ["X-Forwarded-Proto"] = req.Headers["X-Forwarded-Proto"].ToString(),
                ["X-Forwarded-For"] = req.Headers["X-Forwarded-For"].ToString(),
                ["X-Real-IP"] = req.Headers["X-Real-IP"].ToString(),
                ["CF-Connecting-IP"] = req.Headers["CF-Connecting-IP"].ToString(),
                ["User-Agent"] = req.Headers["User-Agent"].ToString()
            };

            var payload = new
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),

                environment = new
                {
                    aspnetcoreEnvironmentVar = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    dotnetEnvironmentVar = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
                    envName = this.env.EnvironmentName,
                    isDevelopment = this.env.IsDevelopment(),
                    isProduction = this.env.IsProduction(),
                    contentRoot = this.env.ContentRootPath,
                    osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    machineName = Environment.MachineName
                },

                request = new
                {
                    scheme = req.Scheme,
                    isHttps = req.IsHttps,
                    hostHost = req.Host.Host,
                    hostValue = req.Host.Value,
                    hostPort = req.Host.Port,
                    path = req.Path.Value,
                    queryString = req.QueryString.Value,
                    pathBase = req.PathBase.Value,
                    method = req.Method,
                    remoteIp = this.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    remotePort = this.HttpContext.Connection.RemotePort,
                    localIp = this.HttpContext.Connection.LocalIpAddress?.ToString(),
                    localPort = this.HttpContext.Connection.LocalPort,
                    headers = headerSnapshot
                },

                cacheValues = new
                {
                    rawAppDomain,
                    rawAppDomainIsNull = rawAppDomain == null,
                    rawAppDomainIsEmpty = string.IsNullOrEmpty(rawAppDomain),
                    rawAppDomainLength = rawAppDomain?.Length,
                    rawAppDomainException = rawAppErr,

                    rawCanonicalDomain,
                    rawCanonicalDomainIsNull = rawCanonicalDomain == null,
                    rawCanonicalDomainIsEmpty = string.IsNullOrEmpty(rawCanonicalDomain),
                    rawCanonicalDomainLength = rawCanonicalDomain?.Length,
                    rawCanonicalDomainException = rawCanonicalErr,

                    torSuffixConstant = StringConstants.TorDomain
                },

                resolverState = new
                {
                    isTor = SafeCall(() => this.urlResolver.IsTor.ToString()),
                    isLocal = SafeCall(() => this.urlResolver.IsLocal.ToString()),
                    baseUrl = SafeCall(() => this.urlResolver.BaseUrl)
                },

                resolveToApp,
                resolveToRoot,

                interpretation = BuildInterpretation(rawAppDomain, this.env, req.Host.Host)
            };

            return this.Json(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private static string SafeCall(Func<string> f)
        {
            try { return f() ?? "<null>"; }
            catch (Exception ex) { return "EXCEPTION: " + ex.Message; }
        }

        private static List<string> BuildInterpretation(string? rawAppDomain, IWebHostEnvironment env, string host)
        {
            var notes = new List<string>();

            if (string.IsNullOrWhiteSpace(rawAppDomain))
            {
                notes.Add("⚠️ AppDomain cache value is null/empty — ResolveToApp will return relative paths. " +
                          "Check SiteConfigEntries row for SiteConfigSetting='AppDomain'.");
            }
            else if (!rawAppDomain.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"⚠️ AppDomain '{rawAppDomain}' is missing scheme (https://). " +
                          "Form actions will be malformed.");
            }
            else
            {
                notes.Add($"✅ AppDomain looks valid: '{rawAppDomain}'.");
            }

            if (env.IsDevelopment())
            {
                notes.Add("⚠️ Environment is Development. If host is localhost/127.0.0.1, IsLocal=true and " +
                          "ResolveToApp returns relative paths. Set ASPNETCORE_ENVIRONMENT=Production in systemd.");
            }

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"⚠️ Request host is '{host}'. If you're hitting this through nginx, nginx is not " +
                          "forwarding the original Host header. Add: proxy_set_header Host $host;");
            }

            return notes;
        }
    }
}