using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Affiliates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Route("affiliates")]
    public class AffiliatesController : Controller
    {
        private readonly IAffiliateAccountRepository affiliateRepo;
        private readonly IAffiliateCommissionRepository commissionRepo;

        public AffiliatesController(
            IAffiliateAccountRepository affiliateRepo,
            IAffiliateCommissionRepository commissionRepo)
        {
            this.affiliateRepo = affiliateRepo;
            this.commissionRepo = commissionRepo;
        }

        [HttpGet("")]
        [AllowAnonymous]
        public IActionResult Index()
        {
            return this.View();
        }

        // GET /affiliates/signup
        [HttpGet("signup")]
        public IActionResult Signup()
        {
            var vm = new AffiliateSignupInputModel
            {
                // pick a sensible default if you want; leaving Unknown to force explicit choice
                PayoutCurrency = Currency.Unknown
            };
            return this.View(vm);
        }

        [HttpPost("signup")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signup(AffiliateSignupInputModel input, CancellationToken ct)
        {
            // CAPTCHA
            var ctx = (this.Request.Form["CaptchaContext"].ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ctx))
            {
                ctx = "affiliate";
            }

            var submittedCaptcha = input?.Captcha ?? this.Request.Form["Captcha"].ToString();
            var captchaOk = CaptchaTools.Validate(this.HttpContext, ctx, submittedCaptcha, consume: true);
            if (!captchaOk)
            {
                this.ModelState.AddModelError("Captcha", "Incorrect CAPTCHA. Please try again.");
                return this.View(input);
            }

            // Normalize / validate referral code
            var raw = input?.ReferralCode ?? string.Empty;
            var trimmed = raw.Trim();
            var code = trimmed.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(code) || code.Length < 3 || code.Length > 12)
            {
                this.ModelState.AddModelError(nameof(input.ReferralCode), "Referral code must be 3–12 characters.");
            }

            // no internal spaces allowed (leading/trailing were trimmed above)
            if (trimmed.Any(char.IsWhiteSpace))
            {
                this.ModelState.AddModelError(nameof(input.ReferralCode), "Referral code cannot contain spaces.");
            }

            // alphanumeric only
            foreach (char c in code)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    this.ModelState.AddModelError(nameof(input.ReferralCode), "Referral code may only contain letters and numbers.");
                    break;
                }
            }

            if (input.PayoutCurrency == Currency.Unknown)
            {
                this.ModelState.AddModelError(nameof(input.PayoutCurrency), "Please choose a payout currency.");
            }

            var wallet = (input?.WalletAddress ?? string.Empty).Trim();
            if (input.PayoutCurrency == Currency.XMR)
            {
                if (!MoneroAddressValidator.IsValid(wallet))
                {
                    this.ModelState.AddModelError(nameof(input.WalletAddress), "Invalid Monero address.");
                }
            }
            else if (string.IsNullOrWhiteSpace(wallet))
            {
                this.ModelState.AddModelError(nameof(input.WalletAddress), "Wallet address is required.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(input);
            }

            // uniqueness check on LOWERCASE code
            if (await this.affiliateRepo.ExistsByReferralCodeAsync(code, ct))
            {
                this.ModelState.AddModelError(nameof(input.ReferralCode), "That referral code is already in use.");
                return this.View(input);
            }

            var entity = new AffiliateAccount
            {
                ReferralCode = code, // stored lowercase
                WalletAddress = wallet,
                PayoutCurrency = input.PayoutCurrency,
                Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim()
            };

            await this.affiliateRepo.CreateAsync(entity, ct);
            return this.View("SignupSuccess", entity);
        }

        // GET /affiliates/commissions  (form + optional results)
        [HttpGet("commissions")]
        public IActionResult Commissions() => this.View(new AffiliateCommissionLookupInputModel());

        // POST /affiliates/commissions  (lookup by referral code + wallet)
        [HttpPost("commissions")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Commissions(AffiliateCommissionLookupInputModel input, CancellationToken ct)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(input);
            }

            var acct = await this.affiliateRepo.GetByCodeAndWalletAsync(
                (input.ReferralCode ?? string.Empty).Trim(),
                (input.WalletAddress ?? string.Empty).Trim(),
                ct);

            if (acct is null)
            {
                this.ModelState.AddModelError(string.Empty, "No affiliate account matches that referral code and wallet address.");
                return this.View(input);
            }

            // You may already have this; if not, add ListForAffiliateAsync to your commission repo
            var commissions = await this.commissionRepo.ListForAffiliateAsync(acct.AffiliateAccountId, ct);

            var vm = new AffiliateCommissionLookupResultModel
            {
                ReferralCode = acct.ReferralCode,
                WalletAddress = acct.WalletAddress,
                PayoutCurrency = acct.PayoutCurrency,
                Email = acct.Email,
                Commissions = commissions
                    .OrderByDescending(c => c.UpdateDate ?? c.CreateDate)
                    .Select(c => new AffiliateCommissionRow
                    {
                        AffiliateCommissionId = c.AffiliateCommissionId,
                        SponsoredListingInvoiceId = c.SponsoredListingInvoiceId,
                        AmountDue = c.AmountDue,
                        PayoutCurrency = c.PayoutCurrency,
                        PayoutStatus = c.PayoutStatus,
                        PayoutTransactionId = c.PayoutTransactionId,
                        CreateDate = c.CreateDate,
                        UpdateDate = c.UpdateDate
                    })
                    .ToList()
            };

            return this.View("CommissionsResults", vm);
        }
    }
}