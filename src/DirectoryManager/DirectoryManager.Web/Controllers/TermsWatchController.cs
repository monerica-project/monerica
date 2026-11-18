using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin view of the monthly terms watcher (DirectoryManager.TermsWatcher): listings whose own
    /// policy pages changed in a way that may make the listing's data wrong. Read-only apart from
    /// "Handled"; corrections are made on the listing's normal edit page.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public class TermsWatchController : Controller
    {
        private const int PageSize = 50;
        private readonly IApplicationDbContext db;

        public TermsWatchController(IApplicationDbContext db)
        {
            this.db = db;
        }

        [HttpGet("admin/terms-watch")]
        public async Task<IActionResult> Index(string? show = null, int page = 1, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            var all = this.db.ListingTermsSnapshots.AsNoTracking();
            IQueryable<ListingTermsSnapshot> query = show switch
            {
                "changed" => all.Where(s => s.ChangedUtc != null),
                "unreadable" => all.Where(s => s.Result == TermsWatchResult.CouldNotRead || s.ReadFailures > 0),
                "all" => all,
                _ => all.Where(s => s.NeedsReview),
            };

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            this.ViewBag.Show = show ?? "review";
            this.ViewBag.Page = page;
            this.ViewBag.PageSize = PageSize;
            this.ViewBag.Total = await query.CountAsync(ct);
            this.ViewBag.NeedsReview = await all.CountAsync(s => s.NeedsReview, ct);
            this.ViewBag.Tracked = await all.CountAsync(ct);
            this.ViewBag.Unreadable = await all.CountAsync(s => s.Result == TermsWatchResult.CouldNotRead || s.ReadFailures > 0, ct);
            this.ViewBag.LastRun = await all.MaxAsync(s => (DateTime?)s.LastCheckedUtc, ct);
            this.ViewBag.AiCallsThisMonth = await this.db.AiCallLogs.CountAsync(l => l.Feature == AiCallLog.FeatureTermsWatch && l.CreatedUtc >= monthStart, ct);

            var rows = await query
                .Include(s => s.DirectoryEntry)
                .OrderByDescending(s => s.NeedsReview)
                .ThenByDescending(s => s.ChangedUtc ?? s.LastCheckedUtc)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync(ct);
            return this.View(rows);
        }

        [HttpPost("admin/terms-watch/{id:int}/handled")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Handled(int id, string? show = null, CancellationToken ct = default)
        {
            var snap = await this.db.ListingTermsSnapshots.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (snap is null)
            {
                return this.NotFound();
            }

            snap.NeedsReview = false;
            snap.HandledUtc = DateTime.UtcNow;
            await this.db.SaveChangesAsync(ct);
            return this.RedirectToAction(nameof(this.Index), new { show });
        }
    }
}
