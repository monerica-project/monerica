using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Processors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [Route("processors")]
    public class ProcessorsController : Controller
    {
        private readonly IProcessorRepository processorRepo;
        private readonly UserManager<ApplicationUser> userManager;

        public ProcessorsController(
            IProcessorRepository processorRepo,
            UserManager<ApplicationUser> userManager)
        {
            this.processorRepo = processorRepo;
            this.userManager = userManager;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var items = await this.processorRepo.ListAllAsync(ct);
            return this.View(items);
        }

        [HttpGet("create")]
        public IActionResult Create()
            => this.View(new ProcessorEditVm());

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProcessorEditVm vm, CancellationToken ct)
        {
            vm.Name = (vm.Name ?? string.Empty).Trim();

            if (!this.ModelState.IsValid)
            {
                return this.View(vm);
            }

            var existing = await this.processorRepo.GetByNameAsync(vm.Name, ct);
            if (existing != null)
            {
                this.ModelState.AddModelError(nameof(vm.Name), "That processor name already exists.");
                return this.View(vm);
            }

            var userId = this.userManager.GetUserId(this.User) ?? string.Empty;

            await this.processorRepo.CreateAsync(new Processor
            {
                Name = vm.Name,
                CreatedByUserId = userId,
                UpdatedByUserId = null
            }, ct);

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("edit/{id:int}")]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var existing = await this.processorRepo.GetByIdAsync(id, ct);
            if (existing is null) return this.NotFound();

            return this.View(new ProcessorEditVm
            {
                ProcessorId = existing.ProcessorId,
                Name = existing.Name
            });
        }

        [HttpPost("edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProcessorEditVm vm, CancellationToken ct)
        {
            vm.Name = (vm.Name ?? string.Empty).Trim();

            if (!this.ModelState.IsValid)
            {
                return this.View(vm);
            }

            var existing = await this.processorRepo.GetByIdAsync(id, ct);
            if (existing is null) return this.NotFound();

            // prevent rename collision
            var dup = await this.processorRepo.GetByNameAsync(vm.Name, ct);
            if (dup != null && dup.ProcessorId != id)
            {
                this.ModelState.AddModelError(nameof(vm.Name), "That processor name already exists.");
                return this.View(vm);
            }

            var userId = this.userManager.GetUserId(this.User) ?? string.Empty;

            await this.processorRepo.UpdateAsync(new Processor
            {
                ProcessorId = id,
                Name = vm.Name,
                UpdatedByUserId = userId
            }, ct);

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpGet("delete/{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var existing = await this.processorRepo.GetByIdAsync(id, ct);
            if (existing is null) return this.NotFound();

            return this.View(existing);
        }

        [HttpPost("delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
        {
            await this.processorRepo.DeleteAsync(id, ct);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}