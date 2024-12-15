using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SponsoredListingOfferController : Controller
    {
        private readonly ISponsoredListingOfferRepository sponsoredListingOfferRepository;
        private readonly ISubcategoryRepository subCategoryRepository;

        public SponsoredListingOfferController(
            ISponsoredListingOfferRepository sponsoredListingOfferRepository,
            ISubcategoryRepository subCategoryRepository)
        {
            this.sponsoredListingOfferRepository = sponsoredListingOfferRepository;
            this.subCategoryRepository = subCategoryRepository;
        }

        [Route("sponsoredlistingoffer/index")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return this.View(await this.sponsoredListingOfferRepository.GetAllAsync());
        }

        [Route("sponsoredlistingoffer/details")]
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var sponsoredListingOffer = await this.sponsoredListingOfferRepository.GetByIdAsync(id);
            if (sponsoredListingOffer == null)
            {
                return this.NotFound();
            }

            return this.View(sponsoredListingOffer);
        }

        [Route("sponsoredlistingoffer/create")]
        [HttpGet]
        public async Task<IActionResult> CreateAsync()
        {
            var subCategories = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync();
            this.ViewBag.SubCategories = subCategories?.OrderBy(sc => sc.Category.Name).ThenBy(sc => sc.Name).ToList()
                                    ?? new List<Subcategory>();
            this.ViewBag.SponsorshipTypeSelectList = new SelectList(Enum.GetValues(typeof(Data.Enums.SponsorshipType)));

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
                await this.sponsoredListingOfferRepository.CreateAsync(sponsoredListingOffer);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(sponsoredListingOffer);
        }

        [HttpGet]
        [Route("sponsoredlistingoffer/edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var sponsoredListingOffer = await this.sponsoredListingOfferRepository.GetByIdAsync(id);
            if (sponsoredListingOffer == null)
            {
                return this.NotFound();
            }

            var subCategories = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync();

            this.ViewBag.SubCategories = subCategories?.OrderBy(sc => sc.Category.Name).ThenBy(sc => sc.Name).ToList()
                        ?? new List<Subcategory>();

            return this.View(sponsoredListingOffer);
        }

        [Route("sponsoredlistingoffer/edit")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SponsoredListingOffer sponsoredListingOffer)
        {
            if (this.ModelState.IsValid)
            {
                await this.sponsoredListingOfferRepository.UpdateAsync(sponsoredListingOffer);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(sponsoredListingOffer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("sponsoredlistingoffer/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await this.sponsoredListingOfferRepository.DeleteOfferAsync(id);
                this.TempData[DirectoryManager.Web.Constants.StringConstants.SuccessMessage] = "The sponsored listing offer has been successfully deleted.";
            }
            catch (Exception ex)
            {
                this.TempData[DirectoryManager.Web.Constants.StringConstants.ErrorMessage] = $"Failed to delete the offer: {ex.Message}";
            }

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}