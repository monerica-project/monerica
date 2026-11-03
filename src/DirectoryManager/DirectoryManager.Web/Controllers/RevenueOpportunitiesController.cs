using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Revenue;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>Admin reports that point at money to be made: abandoned checkouts, renewals, prospects.</summary>
    [Authorize]
    [Route("admin/revenue-opportunities")]
    public class RevenueOpportunitiesController : Controller
    {
        private readonly RevenueOpportunityService service;
        private readonly IRevenueOpportunityRepository repo;
        private readonly IMemoryCache cache;

        public RevenueOpportunitiesController(RevenueOpportunityService service, IRevenueOpportunityRepository repo, IMemoryCache cache)
        {
            this.service = service;
            this.repo = repo;
            this.cache = cache;
        }

        [HttpGet("")]
        public IActionResult Index() => this.RedirectToAction(nameof(this.Abandoned));

        [HttpGet("abandoned")]
        public async Task<IActionResult> Abandoned([FromQuery] int days = RevenueOpportunityService.AbandonedDefaultDays)
        {
            days = Math.Clamp(days, 1, 730);
            var vm = new RevenueOpportunitiesViewModel { Days = days, Abandoned = await this.service.GetAbandonedAsync(days) };
            return this.View(vm);
        }

        [HttpGet("renewals")]
        public async Task<IActionResult> Renewals()
        {
            var (expiring, lapsed) = await this.service.GetRenewalsAsync();
            return this.View(new RevenueOpportunitiesViewModel { Expiring = expiring, Lapsed = lapsed });
        }

        [HttpGet("prospects")]
        public async Task<IActionResult> Prospects()
        {
            return this.View(new RevenueOpportunitiesViewModel
            {
                Prospects = await this.service.GetProspectsAsync(),
                ViewsUpdatedUtc = await this.service.GetViewsUpdatedUtcAsync(),
            });
        }

        /// <summary>Mark one checkout as your own test (or undo it).</summary>
        [HttpPost("test/{invoiceId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetTest(int invoiceId, [FromForm] bool isTest, [FromForm] bool allWithEmail, [FromForm] string? returnUrl)
        {
            var email = await this.repo.SetInvoiceTestAsync(invoiceId, isTest);
            if (allWithEmail && !string.IsNullOrWhiteSpace(email))
            {
                // Also future checkouts with this email are flagged automatically (see checkout).
                await this.repo.SetTestByEmailAsync(email, isTest);
            }

            RevenueOpportunityService.ClearSummaryCache(this.cache);
            return this.LocalRedirect(this.Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/revenue-opportunities/abandoned");
        }
    }
}
