using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Public opt-out page for marketing email. Each recipient gets a private link
    /// (/email-opt-out/{token}); opening it shows one button. The opt-out itself is a POST
    /// so that mail scanners which only fetch links cannot opt anyone out by accident.
    /// </summary>
    [Route("email-opt-out")]
    public class MarketingOptOutController : Controller
    {
        private const string TokenRoute = "{token:regex(^[[a-fA-F0-9]]{{32}}$)}";

        private readonly IMarketingEmailContactRepository repo;

        public MarketingOptOutController(IMarketingEmailContactRepository repo)
        {
            this.repo = repo;
        }

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
