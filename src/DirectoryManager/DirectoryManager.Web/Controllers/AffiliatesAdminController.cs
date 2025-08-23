// Web/Controllers/AffiliatesAdminController.cs
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/affiliates")]
    public class AffiliatesAdminController : Controller
    {
        private readonly IAffiliateAccountRepository affiliateRepo;
        private readonly IAffiliateCommissionRepository commissionRepo;

        public AffiliatesAdminController(
            IAffiliateAccountRepository affiliateRepo,
            IAffiliateCommissionRepository commissionRepo)
        {
            this.affiliateRepo = affiliateRepo;
            this.commissionRepo = commissionRepo;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var affiliates = await this.affiliateRepo.ListAllAsync(ct);
            return this.View(affiliates.ToList().OrderBy(a => a.ReferralCode).ToList());
        }

        // POST /admin/affiliates/find
        [HttpPost("find")]
        public async Task<IActionResult> Find(string referralCode, CancellationToken ct)
        {
            var acct = await this.affiliateRepo.GetByReferralCodeAsync((referralCode ?? string.Empty).Trim(), ct);
            if (acct is null)
            {
                this.ModelState.AddModelError(string.Empty, "No affiliate found with that referral code.");
                return this.View("Index");
            }

            return this.RedirectToAction(nameof(this.Details), new { id = acct.AffiliateAccountId });
        }

        // GET /admin/affiliates/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var acct = await this.affiliateRepo.GetByIdAsync(id, ct);
            if (acct is null) return this.NotFound();

            var commissions = await this.commissionRepo.ListForAffiliateAsync(id, ct);
            this.ViewBag.Account = acct;
            return this.View(commissions.OrderByDescending(c => c.UpdateDate ?? c.CreateDate).ToList());
        }

        // POST /admin/affiliates/commission/{id}/update
        [HttpPost("commission/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCommission(
            int id,
            CommissionPayoutStatus payoutStatus,
            string? payoutTransactionId,
            CancellationToken ct)
        {
            var entity = await this.commissionRepo.GetByIdAsync(id, ct);
            if (entity is null) return this.NotFound();

            entity.PayoutStatus = payoutStatus;
            entity.PayoutTransactionId = string.IsNullOrWhiteSpace(payoutTransactionId) ? null : payoutTransactionId.Trim();

            await this.commissionRepo.UpdateAsync(entity, ct);
            return this.RedirectToAction(nameof(this.Details), new { id = entity.AffiliateAccountId });
        }
    }
}
