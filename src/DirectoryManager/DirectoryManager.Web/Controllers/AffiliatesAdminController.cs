using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            if (acct is null)
            {
                return this.NotFound();
            }

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
            if (entity is null)
            {
                return this.NotFound();
            }

            entity.PayoutStatus = payoutStatus;
            entity.PayoutTransactionId = string.IsNullOrWhiteSpace(payoutTransactionId) ? null : payoutTransactionId.Trim();

            await this.commissionRepo.UpdateAsync(entity, ct);
            return this.RedirectToAction(nameof(this.Details), new { id = entity.AffiliateAccountId });
        }

        [HttpGet("commissions")]
        public async Task<IActionResult> Commissions(CommissionPayoutStatus? status, CancellationToken ct)
        {
            // Load all affiliates so we can show their referral codes next to commissions
            var affiliates = await this.affiliateRepo.ListAllAsync(ct);
            var affiliateCodeById = affiliates.ToDictionary(a => a.AffiliateAccountId, a => a.ReferralCode);

            // Aggregate commissions across all affiliates
            var all = new List<AffiliateCommission>();
            foreach (var a in affiliates)
            {
                var list = await this.commissionRepo.ListForAffiliateAsync(a.AffiliateAccountId, ct);
                if (list != null && list.Count > 0)
                {
                    all.AddRange(list);
                }
            }

            // Optional filter
            var filtered = status.HasValue
                ? all.Where(c => c.PayoutStatus == status.Value).ToList()
                : all;

            var ordered = filtered
                .OrderByDescending(c => c.UpdateDate ?? c.CreateDate)
                .ToList();

            // For badges/filters in the view
            this.ViewBag.FilterStatus = status; // null = all
            this.ViewBag.CountAll = all.Count;
            this.ViewBag.CountPending = all.Count(c => c.PayoutStatus == CommissionPayoutStatus.Pending);
            this.ViewBag.CountPaid = all.Count(c => c.PayoutStatus == CommissionPayoutStatus.Paid);
            this.ViewBag.CountCanceled = all.Count(c => c.PayoutStatus == CommissionPayoutStatus.Canceled);
            this.ViewBag.AffiliateCodeById = affiliateCodeById;

            return this.View("Commissions", ordered);
        }
    }
}