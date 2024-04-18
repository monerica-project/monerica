using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SponsoredListingOfferController : Controller
    {
        private readonly ISponsoredListingOfferRepository repository;

        public SponsoredListingOfferController(
            ISponsoredListingOfferRepository repository)
        {
            this.repository = repository;
        }

        [Route("sponsoredlistingoffer/index")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return this.View(await this.repository.GetAllAsync());
        }

        [Route("sponsoredlistingoffer/details")]
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

        [Route("sponsoredlistingoffer/create")]
        [HttpGet]
        public IActionResult Create()
        {
            return this.View();
        }

        [Route("sponsoredlistingoffer/create")]
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

        [HttpGet]
        [Route("sponsoredlistingoffer/edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var sponsoredListingOffer = await this.repository.GetByIdAsync(id);
            if (sponsoredListingOffer == null)
            {
                return this.NotFound();
            }

            return this.View(sponsoredListingOffer);
        }

        [Route("sponsoredlistingoffer/edit")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SponsoredListingOffer sponsoredListingOffer)
        {
            if (id != sponsoredListingOffer.SponsoredListingOfferId)
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