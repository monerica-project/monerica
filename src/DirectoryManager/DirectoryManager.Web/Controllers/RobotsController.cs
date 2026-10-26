using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class RobotsController : Controller
    {
        /// <summary>AI and answer-engine crawlers that are explicitly allowed.</summary>
        internal static readonly string[] AllowedCrawlers =
        {
            "GPTBot",
            "OAI-SearchBot",
            "ChatGPT-User",
            "ClaudeBot",
            "Claude-SearchBot",
            "Claude-User",
            "anthropic-ai",
            "PerplexityBot",
            "Perplexity-User",
            "Google-Extended",
            "Applebot-Extended",
            "CCBot",
            "Meta-ExternalAgent",
            "Amazonbot",
            "DuckAssistBot",
            "MistralAI-User",
            "cohere-ai",
        };

        private readonly ICacheService cacheService;

        public RobotsController(ICacheService cacheService)
        {
            this.cacheService = cacheService;
        }

        [Route("robots.txt")]
        [HttpGet]
        public async Task<ContentResult> RobotsTxt()
        {
            var domain = (await this.cacheService.GetSnippetAsync(SiteConfigSetting.CanonicalDomain))
                .TrimEnd('/');

            var sb = new StringBuilder();

            sb.AppendLine($"# A plain-text summary of this directory for AI tools: {domain}/llms.txt");
            sb.AppendLine($"# Every listing with key facts: {domain}/llms-full.txt");
            sb.AppendLine();

            // Search and AI crawlers are explicitly welcome. Each named group gets the same
            // disallows as everyone else (a crawler follows only its most specific group).
            foreach (var agent in AllowedCrawlers)
            {
                sb.AppendLine($"User-agent: {agent}");
            }

            AppendRules(sb);
            sb.AppendLine();

            sb.AppendLine("User-agent: *");
            AppendRules(sb);
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {domain}/sitemap.xml");

            return this.Content(sb.ToString(), "text/plain");
        }

        private static void AppendRules(StringBuilder sb)
        {
            sb.AppendLine("Allow: /");
            sb.AppendLine("# Admin and management routes");
            sb.AppendLine("Disallow: /directoryentry/");
            sb.AppendLine("Disallow: /subcategory/");
            sb.AppendLine("Disallow: /category/create");
            sb.AppendLine("Disallow: /category/edit");
            sb.AppendLine("Disallow: /category/index");
            sb.AppendLine("Disallow: /admin/");
            sb.AppendLine("Disallow: /sponsoredlisting/create");
            sb.AppendLine("Disallow: /sponsoredlisting/delete/");
            sb.AppendLine("Disallow: /submission/index");
            sb.AppendLine("Disallow: /submission/pending");
            sb.AppendLine("Disallow: /submission/audit/");
            sb.AppendLine("Disallow: /trafficlog/");
            sb.AppendLine("Disallow: /user/");
            sb.AppendLine("Disallow: /emailmanagement/");
        }
    }
}