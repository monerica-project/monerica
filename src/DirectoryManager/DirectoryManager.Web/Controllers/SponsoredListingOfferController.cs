using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingOfferController : Controller
    {
        private readonly ISponsoredListingOfferRepository repository;

        public SponsoredListingOfferController(
            ISponsoredListingOfferRepository repository)
        {
            this.repository = repository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return this.View(await this.repository.GetAllAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var sponsoredListingOffer = await this.repository.GetByIdAsync(id);
            if (sponsoredListingOffer == null)
            {
                return this.NotFound();
            }

            return this.View(sponsoredListingOffer);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return this.View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SponsoredListingOffer sponsoredListingOffer)
        {
            if (this.ModelState.IsValid)
            {
                sponsoredListingOffer.PriceCurrency = Data.Enums.Currency.USD;
                await this.repository.CreateAsync(sponsoredListingOffer);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(sponsoredListingOffer);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var sponsoredListingOffer = await this.repository.GetByIdAsync(id);
            if (sponsoredListingOffer == null)
            {
                return this.NotFound();
            }

            return this.View(sponsoredListingOffer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SponsoredListingOffer sponsoredListingOffer)
        {
            if (id != sponsoredListingOffer.Id)
            {
                return this.NotFound();
            }

            if (this.ModelState.IsValid)
            {
                await this.repository.UpdateAsync(sponsoredListingOffer);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(sponsoredListingOffer);
        }
    }
}