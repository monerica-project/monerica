using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin list of websites that submission links may not point to (e.g. file hosts like
    /// muse.ai). Blocking a domain also blocks its subdomains.
    /// </summary>
    [Authorize]
    public class SubmissionBlockedDomainsController : Controller
    {
        private readonly ISubmissionBlockedDomainRepository repository;

        public SubmissionBlockedDomainsController(ISubmissionBlockedDomainRepository repository)
        {
            this.repository = repository;
        }

        [Route("submissionblockeddomains")]
        [Route("submissionblockeddomains/index")]
        public async Task<IActionResult> Index()
        {
            return this.View(await this.repository.GetAllAsync());
        }

        [Route("submissionblockeddomains/create")]
        public IActionResult Create()
        {
            return this.View(new SubmissionBlockedDomain { Domain = string.Empty });
        }

        [Route("submissionblockeddomains/create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionBlockedDomain model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            if (await this.repository.CreateAsync(model) == null)
            {
                this.ModelState.AddModelError(
                    nameof(model.Domain),
                    "That isn't a valid domain, or it's already in the list.");
                return this.View(model);
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("submissionblockeddomains/toggle")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            await this.repository.ToggleEnabledAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("submissionblockeddomains/delete")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await this.repository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
