using System.Net.Mail;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("admin/marketing-opt-out")]
    public class MarketingOptOutAdminController : BaseController
    {
        private readonly IMarketingEmailContactRepository repo;

        public MarketingOptOutAdminController(
            IMarketingEmailContactRepository repo,
            IMemoryCache cache)
            : base(cache)
        {
            this.repo = repo;
        }

        [HttpGet("")]
        [HttpGet("page/{page:int}")]
        public async Task<IActionResult> Index(int page = 1, bool all = false, int pageSize = 50)
        {
            var vm = new PagedResult<MarketingEmailContact>
            {
                Items = await this.repo.ListPageAsync(page, pageSize, optedOutOnly: !all),
                TotalCount = await this.repo.CountAsync(optedOutOnly: !all)
            };

            this.ViewBag.Page = page;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.All = all;
            this.ViewBag.OptedOutCount = all ? await this.repo.CountAsync(optedOutOnly: true) : vm.TotalCount;
            this.ViewBag.LinkBase = $"{this.Request.Scheme}://{this.Request.Host}/email-opt-out/";
            return this.View(vm);
        }

        // Adds the address if it is new, then either opts it out or just leaves it with a link.
        [HttpPost("add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string email, string? note, bool optOut = true)
        {
            email = (email ?? string.Empty).Trim();
            if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            {
                this.TempData["MarketingOptOutMessage"] = "That isn't a valid email address.";
                return this.RedirectToAction(nameof(this.Index));
            }

            var contact = await this.repo.GetOrCreateAsync(email, note);
            if (optOut)
            {
                await this.repo.SetOptedOutAsync(contact.MarketingEmailContactId, true, MarketingEmailContact.SourceAdmin);
            }

            this.TempData["MarketingOptOutMessage"] = optOut
                ? $"{contact.Email} is opted out."
                : $"{contact.Email} has an opt-out link.";
            return this.RedirectToAction(nameof(this.Index), new { all = !optOut });
        }

        [HttpPost("set/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Set(int id, bool optOut, bool all = false)
        {
            await this.repo.SetOptedOutAsync(id, optOut, MarketingEmailContact.SourceAdmin);
            return this.RedirectToAction(nameof(this.Index), new { all });
        }
    }
}
