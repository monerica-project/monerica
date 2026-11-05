using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Public opt-out pages for marketing email.
    /// - /email-opt-out: anyone types their address (captcha-protected). Works for every email we
    ///   send, so emails never need a per-recipient link filled in.
    /// - /email-opt-out/{token}: a recipient's private one-click link (one button, POST), kept for
    ///   emails that already went out with it.
    /// Opting out is always a POST so mail scanners that only fetch links can't opt anyone out.
    /// </summary>
    [Route("email-opt-out")]
    public class MarketingOptOutController : Controller
    {
        public const string CaptchaContext = "marketing-opt-out";
        private const string TokenRoute = "{token:regex(^[[a-fA-F0-9]]{{32}}$)}";

        private readonly IMarketingEmailContactRepository repo;

        public MarketingOptOutController(IMarketingEmailContactRepository repo)
        {
            this.repo = repo;
        }

        // ------------------------------------------------------------ generic "enter your email" page
        [HttpGet("")]
        public IActionResult Generic([FromQuery] string? email)
        {
            this.ViewBag.PrefillEmail = (email ?? string.Empty).Trim();
            return this.View("Generic", null);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenericPost([FromForm] string? formEmail)
        {
            this.ViewBag.PrefillEmail = (formEmail ?? string.Empty).Trim();

            if (!CaptchaTools.Validate(this.HttpContext, CaptchaContext, this.Request.Form["Captcha"].ToString(), consume: true))
            {
                this.ModelState.AddModelError(string.Empty, "Incorrect CAPTCHA. Please try again.");
                return this.View("Generic", null);
            }

            var (ok, normalized, error) = EmailValidationHelper.Validate(formEmail);
            if (!ok)
            {
                this.ModelState.AddModelError(string.Empty, error ?? "Please enter a valid email address.");
                return this.View("Generic", null);
            }

            // Same result whether or not we had the address: it is recorded as opted out either way,
            // so future emails skip it, and the page never reveals who is on our list.
            var contact = await this.repo.GetOrCreateAsync(normalized!, "opted out on the public opt-out page");
            await this.repo.SetOptedOutAsync(contact.MarketingEmailContactId, true, MarketingEmailContact.SourceForm);
            return this.View("Generic", normalized);
        }

        // ------------------------------------------------------------ private one-click links
        [HttpGet(TokenRoute)]
        public async Task<IActionResult> Index(string token)
        {
            var contact = await this.repo.GetByTokenAsync(token);
            return this.View("Index", contact);
        }

        [HttpPost(TokenRoute)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OptOut(string token)
        {
            var contact = await this.repo.GetByTokenAsync(token);
            if (contact != null)
            {
                await this.repo.SetOptedOutAsync(contact.MarketingEmailContactId, true, MarketingEmailContact.SourceLink);
            }

            return this.RedirectToAction(nameof(this.Index), new { token });
        }

        [HttpPost(TokenRoute + "/undo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Undo(string token)
        {
            var contact = await this.repo.GetByTokenAsync(token);
            if (contact != null)
            {
                await this.repo.SetOptedOutAsync(contact.MarketingEmailContactId, false, MarketingEmailContact.SourceLink);
            }

            return this.RedirectToAction(nameof(this.Index), new { token });
        }
    }
}
