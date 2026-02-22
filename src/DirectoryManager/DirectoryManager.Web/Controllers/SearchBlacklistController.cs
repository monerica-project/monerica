// Web/Controllers/SearchBlacklistController.cs
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/blacklist")]
    public class SearchBlacklistController : BaseController
    {
        private readonly ISearchBlacklistRepository repo;
        private readonly ISearchBlacklistCache blacklistCache;

        public SearchBlacklistController(
            ISearchBlacklistRepository repo,
            ISearchBlacklistCache blacklistCache,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.repo = repo;
            this.blacklistCache = blacklistCache;
        }

        [HttpGet("")]
        [HttpGet("page/{page:int}")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 50)
        {
            var total = await this.repo.CountAsync();
            var items = await this.repo.ListPageAsync(page, pageSize);

            var vm = new PagedResult<SearchBlacklistTerm>
            {
                Items = items,
                TotalCount = total
            };

            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            return this.View(vm);
        }

        [HttpGet("create")]
        public IActionResult Create() => this.View();

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(string term, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                this.ModelState.AddModelError(string.Empty, "Term is required.");
                return this.View("Create");
            }

            await this.repo.CreateAsync(term.Trim());

            // ensure Search + Reviews + Replies see the update immediately
            this.blacklistCache.Invalidate();

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            await this.repo.DeleteAsync(id);

            // ensure Search + Reviews + Replies see the update immediately
            this.blacklistCache.Invalidate();

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}