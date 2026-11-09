using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>Admin view of every AI call (what was sent, what came back, what the job did).</summary>
    [Authorize]
    public class AiCallLogController : Controller
    {
        private const int PageSize = 50;
        private readonly IAiCallLogRepository repo;

        public AiCallLogController(IAiCallLogRepository repo)
        {
            this.repo = repo;
        }

        [HttpGet("admin/ai-log")]
        public async Task<IActionResult> Index(int page = 1, int? submissionId = null, bool errors = false, CancellationToken ct = default)
        {
            var (items, total) = await this.repo.GetPageAsync(page, PageSize, submissionId, errors, ct);
            var vm = new AiCallLogListViewModel
            {
                Items = items,
                Total = total,
                Page = Math.Max(1, page),
                PageSize = PageSize,
                SubmissionId = submissionId,
                ErrorsOnly = errors,
                Daily = await this.repo.GetDailyTotalsAsync(30, ct),
            };
            return this.View(vm);
        }

        [HttpGet("admin/ai-log/{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var log = await this.repo.GetByIdAsync(id, ct);
            return log == null ? this.NotFound() : this.View(log);
        }
    }
}
