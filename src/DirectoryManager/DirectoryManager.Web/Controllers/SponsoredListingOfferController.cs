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
        private readonly ICategoryRepository categoryRepository;

        public SponsoredListingOfferController(
            ISponsoredListingOfferRepository sponsoredListingOfferRepository,
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository)
        {
            this.sponsoredListingOfferRepository = sponsoredListingOfferRepository;
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
        }

        [Route("sponsoredlistingoffer/index")]
        [Route("sponsoredlistingoffers")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var offers = await this.sponsoredListingOfferRepository
                                  .GetAllAsync()
                                  .ConfigureAwait(false);

            return this.View(offers);
        }

        [Route("sponsoredlistingoffer/details")]
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var offer = await this.sponsoredListingOfferRepository
                                   .GetByIdAsync(id)
                                   .ConfigureAwait(false);
            if (offer == null)
            {
                return this.NotFound();
            }

            return this.View(offer);
        }

        // GET: /sponsoredlistingoffer/create
        [Route("sponsoredlistingoffer/create")]
        [HttpGet]
        public async Task<IActionResult> CreateAsync()
        {
            var subCategories = await this.subCategoryRepository
                                      .GetAllActiveSubCategoriesAsync()
                                      .ConfigureAwait(false);
            var categories = await this.categoryRepository
                                         .GetAllAsync()
                                         .ConfigureAwait(false);

            this.ViewBag.SubCategories = subCategories
                                         .OrderBy(sc => sc.Category.Name)
                                         .ThenBy(sc => sc.Name)
                                         .ToList();
            this.ViewBag.Categories = categories
                                        .OrderBy(c => c.Name)
                                        .ToList();

            this.ViewBag.SponsorshipTypeSelectList = new SelectList(
                Enum.GetValues(typeof(Data.Enums.SponsorshipType)));

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
                await this.sponsoredListingOfferRepository
                         .CreateAsync(sponsoredListingOffer)
                         .ConfigureAwait(false);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(sponsoredListingOffer);
        }

        [HttpGet]
        [Route("sponsoredlistingoffer/edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var offer = await this.sponsoredListingOfferRepository
                                   .GetByIdAsync(id)
                                   .ConfigureAwait(false);
            if (offer == null)
            {
                return this.NotFound();
            }

            var subCategories = await this.subCategoryRepository
                                      .GetAllActiveSubCategoriesAsync()
                                      .ConfigureAwait(false);
            var categories = await this.categoryRepository
                                         .GetAllAsync()
                                         .ConfigureAwait(false);

            this.ViewBag.SubCategories = subCategories
                                         .OrderBy(sc => sc.Category.Name)
                                         .ThenBy(sc => sc.Name)
                                         .ToList();
            this.ViewBag.Categories = categories
                                        .OrderBy(c => c.Name)
                                        .ToList();

            this.ViewBag.SponsorshipTypeSelectList = new SelectList(
            Enum.GetValues(typeof(Data.Enums.SponsorshipType)),
            offer.SponsorshipType);

            return this.View(offer);
        }

        [Route("sponsoredlistingoffer/edit")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SponsoredListingOffer sponsoredListingOffer)
        {
            if (this.ModelState.IsValid)
            {
                await this.sponsoredListingOfferRepository
                         .UpdateAsync(sponsoredListingOffer)
                         .ConfigureAwait(false);
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
                await this.sponsoredListingOfferRepository
                         .DeleteOfferAsync(id)
                         .ConfigureAwait(false);
                this.TempData[Constants.StringConstants.SuccessMessage] =
                    "The sponsored listing offer has been successfully deleted.";
            }
            catch (Exception ex)
            {
                this.TempData[Constants.StringConstants.ErrorMessage] =
                    $"Failed to delete the offer: {ex.Message}";
            }

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}