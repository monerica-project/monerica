using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class ProcessorConfigController : Controller
    {
        private readonly IProcessorConfigRepository repository;

        public ProcessorConfigController(IProcessorConfigRepository repository)
        {
            this.repository = repository;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return this.View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProcessorConfig processorConfigs)
        {
            if (this.ModelState.IsValid)
            {
                await this.repository.CreateAsync(processorConfigs);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(processorConfigs);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(PaymentProcessor paymentProcessor)
        {
            var processorConfig = await this.repository.GetByProcessorAsync(paymentProcessor);
            if (processorConfig == null)
            {
                return this.NotFound();
            }

            return this.View(processorConfig);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProcessorConfig processorConfigs)
        {
            if (this.ModelState.IsValid)
            {
                await this.repository.UpdateAsync(processorConfigs);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(processorConfigs);
        }

        public async Task<IActionResult> Index()
        {
            var processorConfigs = await this.repository.GetAllAsync();
            return this.View(processorConfigs);
        }
    }
}
