using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class RobotsController : Controller
    {
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

            sb.AppendLine("User-agent: *");
            sb.AppendLine("Allow: /");
            sb.AppendLine();
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
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {domain}/sitemap.xml");

            return this.Content(sb.ToString(), "text/plain");
        }
    }
}