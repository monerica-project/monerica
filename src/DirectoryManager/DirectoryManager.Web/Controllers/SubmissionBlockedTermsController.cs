using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin CRUD for the submission blacklist — phrases that are rejected on the public
    /// submit page (e.g. "no*aml"). Kept deliberately simple so terms can be added as new
    /// patterns of unwanted wording show up.
    /// </summary>
    [Authorize]
    public class SubmissionBlockedTermsController : Controller
    {
        private readonly ISubmissionBlockedTermRepository repository;

        public SubmissionBlockedTermsController(ISubmissionBlockedTermRepository repository)
        {
            this.repository = repository;
        }

        [Route("submissionblockedterms")]
        [Route("submissionblockedterms/index")]
        public async Task<IActionResult> Index()
        {
            var terms = await this.repository.GetAllAsync();
            return this.View(terms);
        }

        [Route("submissionblockedterms/create")]
        public IActionResult Create()
        {
            return this.View(new SubmissionBlockedTerm { Term = string.Empty });
        }

        [Route("submissionblockedterms/create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubmissionBlockedTerm model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var created = await this.repository.CreateAsync(model);

            if (created == null)
            {
                this.ModelState.AddModelError(
                    nameof(model.Term),
                    "That term is empty or already in the list.");
                return this.View(model);
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("submissionblockedterms/toggle")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            await this.repository.ToggleEnabledAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }

        [Route("submissionblockedterms/delete")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await this.repository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
