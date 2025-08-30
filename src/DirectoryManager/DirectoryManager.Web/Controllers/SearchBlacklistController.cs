// Web/Controllers/SearchBlacklistController.cs
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/blacklist")]
    public class SearchBlacklistController : Controller
    {
        private readonly ISearchBlacklistRepository repo;
        private readonly IMemoryCache cache;

        public SearchBlacklistController(ISearchBlacklistRepository repo, IMemoryCache cache)
        {
            this.repo = repo;
            this.cache = cache;
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
        public async Task<IActionResult> CreatePost(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                this.ModelState.AddModelError("", "Term is required.");
                return this.View("Create");
            }

            await this.repo.CreateAsync(term.Trim());
            await this.RefreshCacheAsync();
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await this.repo.DeleteAsync(id);
            await this.RefreshCacheAsync();
            return this.RedirectToAction(nameof(this.Index));
        }

        private async Task RefreshCacheAsync()
        {
            var terms = await this.repo.GetAllTermsAsync();
            var norm = new HashSet<string>(terms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant()));

            _ = this.cache.Set(
                StringConstants.CacheKeySearchBlacklistTerms, norm, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });
        }
    }
}
