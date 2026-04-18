using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.SponsoredListing;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingController : BaseController
    {
        private static readonly SemaphoreSlim SponsorJsonLock = new (1, 1);
        private static readonly TimeSpan SponsorJsonTtl = TimeSpan.FromSeconds(60);

        private static object? sponsorJsonCache;
        private static DateTimeOffset sponsorJsonCachedAt;

        private readonly ISubcategoryRepository subCategoryRepository;
        private readonly ICategoryRepository categoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly ISponsoredListingOfferRepository sponsoredListingOfferRepository;
        private readonly ISponsoredListingReservationRepository sponsoredListingReservationRepository;
        private readonly ICacheService cacheService;
        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly IUrlResolutionService urlResolutionService;
        private readonly ILogger<SponsoredListingController> logger;

        public SponsoredListingController(
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            ISponsoredListingOfferRepository sponsoredListingOfferRepository,
            ISponsoredListingReservationRepository sponsoredListingReservationRepository,
            ICacheService cacheService,
            IDirectoryEntryReviewRepository reviewRepository,
            IUrlResolutionService urlResolutionService,
            ILogger<SponsoredListingController> logger)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.subCategoryRepository = subCategoryRepository;
            this.categoryRepository = categoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.sponsoredListingOfferRepository = sponsoredListingOfferRepository;
            this.sponsoredListingReservationRepository = sponsoredListingReservationRepository;
            this.cacheService = cacheService;
            this.reviewRepository = reviewRepository;
            this.urlResolutionService = urlResolutionService;
            this.logger = logger;
        }

        // =====================================================================
        // INDEX
        // =====================================================================

        [Route("advertise")]
        [Route("advertising")]
        [Route("sponsoredlisting")]
        public async Task<IActionResult> IndexAsync()
        {
            var mainType = SponsorshipType.MainSponsor;
            var mainGroup = ReservationGroupHelper.BuildReservationGroupName(mainType, 0);
            var currentMain = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(mainType);
            var model = new SponsoredListingHomeModel();

            if (currentMain != null && currentMain.Any())
            {
                var count = currentMain.Count();
                model.CurrentListingCount = count;

                if (count >= Common.Constants.IntegerConstants.MaxMainSponsoredListings)
                {
                    model.CanCreateMainListing = false;
                }
                else
                {
                    var active = await this.sponsoredListingRepository.GetActiveSponsorsCountAsync(mainType, null);
                    var reserved = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync(mainGroup);

                    model.CanCreateMainListing = SponsoredListingCheckoutHelper.CanPurchaseListing(active, reserved, mainType);
                    if (!model.CanCreateMainListing)
                    {
                        model.Message = await this.BuildMainSponsorUnavailableMessageAsync(active, mainGroup);
                    }
                }

                model.NextListingExpiration = currentMain.Min(x => x.CampaignEndDate);
            }
            else
            {
                model.CanCreateMainListing = true;
            }

            await this.PopulateSubcategoryAvailabilityAsync(model);
            await this.PopulateCategoryAvailabilityAsync(model);
            return this.View(model);
        }

        // =====================================================================
        // OFFERS
        // =====================================================================

        [AllowAnonymous]
        [Route("sponsoredlisting/offers")]
        [HttpGet]
        public async Task<IActionResult> Offers()
        {
            var mainOffers = await this.sponsoredListingOfferRepository.GetAllByTypeAsync(SponsorshipType.MainSponsor);
            var categoryOffers = await this.sponsoredListingOfferRepository.GetAllByTypeAsync(SponsorshipType.CategorySponsor);
            var subcategoryOffers = await this.sponsoredListingOfferRepository.GetAllByTypeAsync(SponsorshipType.SubcategorySponsor);

            var mainActiveCount = (await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor)).Count();
            var activeByCategory = await this.sponsoredListingRepository.GetActiveSponsorCountByCategoryAsync(SponsorshipType.CategorySponsor);
            var activeBySubcategory = await this.sponsoredListingRepository.GetActiveSponsorCountBySubcategoryAsync(SponsorshipType.SubcategorySponsor);

            var allCats = await this.categoryRepository.GetActiveCategoriesAsync();
            var allSubcats = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync();

            var freeCategories = allCats.Where(c => !activeByCategory.ContainsKey(c.CategoryId)).Select(c => c.CategoryId).ToHashSet();
            var freeSubcategories = allSubcats.Where(sc => !activeBySubcategory.ContainsKey(sc.SubCategoryId)).Select(sc => sc.SubCategoryId).ToHashSet();

            var model = new SponsoredListingOffersViewModel
            {
                MainSponsorshipOffers = this.BuildMainOfferModels(mainOffers, mainActiveCount),
                CategorySponsorshipOffers = this.BuildCategoryOfferModels(categoryOffers, freeCategories),
                SubCategorySponsorshipOffers = this.BuildSubcategoryOfferModels(subcategoryOffers, freeSubcategories),
                ConversionRate = 0,
                SelectedCurrency = "XMR",
                LastUpdatedDate = await this.sponsoredListingOfferRepository.GetLastModifiedDateAsync(),
            };

            return this.View(model);
        }

        // =====================================================================
        // CURRENT / ACTIVE LISTINGS / JSON / LIST
        // =====================================================================

        [AllowAnonymous]
        [Route("sponsoredlisting/current")]
        [HttpGet]
        public IActionResult Current() => this.View();

        [Route("sponsoredlisting/activelistings")]
        [HttpGet]
        public async Task<IActionResult> ActiveListings()
        {
            var listings = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();

            var model = new ActiveSponsoredListingViewModel
            {
                MainSponsorItems = listings
                    .Where(l => l.SponsorshipType == SponsorshipType.MainSponsor)
                    .Select(l => this.ToActiveModel(l))
                    .ToList(),
                SubCategorySponsorItems = listings
                    .Where(l => l.SponsorshipType == SponsorshipType.SubcategorySponsor)
                    .Select(l => this.ToActiveModel(l, subcategoryName: this.ResolveSubcategoryName(l.SubCategoryId)))
                    .ToList(),
                CategorySponsorItems = listings
                    .Where(l => l.SponsorshipType == SponsorshipType.CategorySponsor)
                    .Select(l => this.ToActiveModel(l, categoryName: this.ResolveCategoryName(l.CategoryId)))
                    .ToList(),
            };

            return this.View("activelistings", model);
        }

        [AllowAnonymous]
        [Route("sponsoredlisting/activesponsorjson")]
        [HttpGet]
        public async Task<IActionResult> ActiveSponsorJsonAsync()
        {
            this.Response.Headers["Access-Control-Allow-Origin"] = "*";
            var ct = this.HttpContext.RequestAborted;

            if (sponsorJsonCache != null && DateTimeOffset.UtcNow - sponsorJsonCachedAt < SponsorJsonTtl)
            {
                return this.Json(sponsorJsonCache);
            }

            await SponsorJsonLock.WaitAsync(ct);
            try
            {
                if (sponsorJsonCache != null && DateTimeOffset.UtcNow - sponsorJsonCachedAt < SponsorJsonTtl)
                    return this.Json(sponsorJsonCache);

                sponsorJsonCache = await this.BuildActiveSponsorJsonAsync(ct);
                sponsorJsonCachedAt = DateTimeOffset.UtcNow;
                return this.Json(sponsorJsonCache);
            }
            finally { SponsorJsonLock.Release(); }
        }

        [Route("sponsoredlisting/list/{page?}")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> List(int page = 1)
        {
            var pageSize = IntegerConstants.DefaultPageSize;
            var total = await this.sponsoredListingRepository.GetTotalCountAsync();
            var listings = await this.sponsoredListingRepository.GetPaginatedListingsAsync(page, pageSize);

            var model = new PaginatedListingsViewModel
            {
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Listings = listings.Select(l => new ListingViewModel
                {
                    Id = l.SponsoredListingId,
                    DirectoryEntryName = l.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsorshipType = l.SponsorshipType,
                    StartDate = l.CampaignStartDate,
                    EndDate = l.CampaignEndDate,
                }).ToList(),
            };

            return this.View(model);
        }

        // =====================================================================
        // EDIT
        // =====================================================================

        [Route("sponsoredlisting/edit/{sponsoredListingId}")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditAsync(int sponsoredListingId)
        {
            var listing = await this.sponsoredListingRepository.GetByIdAsync(sponsoredListingId);
            if (listing == null) return this.NotFound();

            var entry = await this.directoryEntryRepository.GetByIdAsync(listing.DirectoryEntryId);

            return this.View(new EditListingViewModel
            {
                Id = listing.SponsoredListingId,
                CampaignStartDate = listing.CampaignStartDate,
                CampaignEndDate = listing.CampaignEndDate,
                SponsorshipType = listing.SponsorshipType,
                Name = entry.Name,
                SponsoredListingInvoiceId = listing.SponsoredListingInvoiceId,
            });
        }

        [Route("sponsoredlisting/edit/{sponsoredListingId}")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditAsync(int sponsoredListingId, EditListingViewModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var listing = await this.sponsoredListingRepository.GetByIdAsync(sponsoredListingId);
            if (listing == null)
            {
                return this.NotFound();
            }

            listing.CampaignStartDate = model.CampaignStartDate;
            listing.CampaignEndDate = model.CampaignEndDate;
            await this.sponsoredListingRepository.UpdateAsync(listing);
            this.ClearCachedItems();

            return this.RedirectToAction("List");
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================

        private async Task<string> BuildMainSponsorUnavailableMessageAsync(int totalActive, string mainGroup)
        {
            var max = SponsoredListingCheckoutHelper.GetMaxSlotsForType(SponsorshipType.MainSponsor);

            if (totalActive >= max)
            {
                var next = await this.sponsoredListingRepository
                    .GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor)
                    .ContinueWith(t => t.Result
                        .Where(x => x.CampaignEndDate > DateTime.UtcNow)
                        .OrderBy(x => x.CampaignEndDate)
                        .Select(x => (DateTime?)x.CampaignEndDate)
                        .FirstOrDefault());

                return next.HasValue
                    ? $"All {max} main sponsor spots are currently taken. Next opening expected around {next.Value:yyyy-MM-dd HH:mm} UTC."
                    : $"All {max} main sponsor spots are currently taken.";
            }

            var exp = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(mainGroup);
            if (exp.HasValue)
            {
                var mins = Math.Max(1, (int)Math.Ceiling((exp.Value - DateTime.UtcNow).TotalMinutes));
                return $"A checkout is currently in progress and will expire at {exp.Value:yyyy-MM-dd HH:mm} UTC (in {mins} minutes).";
            }

            return "A checkout is currently in progress for a main sponsor spot.";
        }

        private async Task PopulateSubcategoryAvailabilityAsync(SponsoredListingHomeModel model)
        {
            var allActiveSubs = await this.subCategoryRepository.GetAllActiveSubCategoriesAsync(Common.Constants.IntegerConstants.MinRequiredSubcategories);
            var currentSubSponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.SubcategorySponsor);
            if (currentSubSponsors == null)
            {
                return;
            }

            foreach (var sc in allActiveSubs)
            {
                var label = FormattingHelper.SubcategoryFormatting(sc.Category.Name, sc.Name);
                var sponsor = currentSubSponsors.FirstOrDefault(x => x.SubCategoryId == sc.SubCategoryId);

                if (sponsor != null) { model.UnavailableSubCatetgories.Add(sc.SubCategoryId, label); model.UnavailableSubcategoryExpirations[sc.SubCategoryId] = sponsor.CampaignEndDate; }
                else
                {
                    model.AvailableSubCatetgories.Add(sc.SubCategoryId, label);
                }
            }

            model.AvailableSubCatetgories = model.AvailableSubCatetgories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            model.UnavailableSubCatetgories = model.UnavailableSubCatetgories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private async Task PopulateCategoryAvailabilityAsync(SponsoredListingHomeModel model)
        {
            var allCategories = await this.categoryRepository.GetAllAsync();
            var currentCatSponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(SponsorshipType.CategorySponsor);
            if (currentCatSponsors == null)
            {
                return;
            }

            foreach (var cat in allCategories)
            {
                var sponsor = currentCatSponsors.FirstOrDefault(x =>
                    x.DirectoryEntry?.SubCategory != null &&
                    x.DirectoryEntry.SubCategory.CategoryId == cat.CategoryId);

                if (sponsor != null) { model.UnavailableCategories.Add(cat.CategoryId, cat.Name); model.UnavailableCategoryExpirations[cat.CategoryId] = sponsor.CampaignEndDate; }
                else
                {
                    model.AvailableCategories.Add(cat.CategoryId, cat.Name);
                }
            }

            model.AvailableCategories = model.AvailableCategories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            model.UnavailableCategories = model.UnavailableCategories.OrderBy(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private async Task<List<object>> BuildActiveSponsorJsonAsync(CancellationToken ct)
        {
            var canonicalDomain = await this.cacheService.GetSnippetAsync(Data.Enums.SiteConfigSetting.CanonicalDomain);
            var listings = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();
            var result = new List<object>();

            foreach (var l in listings)
            {
                var e = l.DirectoryEntry;
                if (e == null)
                {
                    continue;
                }

                var reviewCount = await this.reviewRepository.CountApprovedForEntryAsync(e.DirectoryEntryId, ct);
                var reviewRating = await this.reviewRepository.AverageRatingForEntryApprovedAsync(e.DirectoryEntryId, ct);

                result.Add(new
                {
                    name = e.Name ?? string.Empty,
                    link = e.Link ?? string.Empty,
                    expirationDate = l.CampaignEndDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    reviewRating = reviewRating.HasValue ? Math.Round(reviewRating.Value, 1, MidpointRounding.AwayFromZero) : (double?)null,
                    reviewCount,
                    reviewLink = UrlBuilder.CombineUrl(canonicalDomain, UrlBuilder.ListingReviewsPath(e.DirectoryEntryKey)),
                    description = e.Description ?? string.Empty,
                    note = e.Note ?? string.Empty,
                    sponsorshipType = l.SponsorshipType.ToString(),
                });
            }

            return result;
        }

        private List<SponsoredListingOfferDisplayModel> BuildMainOfferModels(IEnumerable<SponsoredListingOffer> offers, int mainActiveCount) =>
            offers.Select(o =>
            {
                var available = mainActiveCount < Common.Constants.IntegerConstants.MaxMainSponsoredListings;
                return new SponsoredListingOfferDisplayModel
                {
                    Description = o.Description,
                    Days = o.Days,
                    Price = o.Price,
                    PriceCurrency = o.PriceCurrency,
                    SponsorshipType = o.SponsorshipType,
                    CategorySubcategory = o.Subcategory != null ? FormattingHelper.SubcategoryFormatting(o.Subcategory.Category!.Name, o.Subcategory.Name) : StringConstants.Default,
                    IsAvailable = available,
                    ActionLink = available
                        ? this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.MainSponsor })
                        : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.MainSponsor }),
                };
            }).OrderBy(o => o.CategorySubcategory).ThenBy(o => o.Days).ToList();

        private List<SponsoredListingOfferDisplayModel> BuildCategoryOfferModels(IEnumerable<SponsoredListingOffer> offers, HashSet<int> freeCategories) =>
            offers.Select(o =>
            {
                var isDefault = o.Subcategory == null;
                var catId = o.Subcategory?.CategoryId ?? 0;
                var available = isDefault || freeCategories.Contains(catId);
                return new SponsoredListingOfferDisplayModel
                {
                    Description = o.Description,
                    Days = o.Days,
                    Price = o.Price,
                    PriceCurrency = o.PriceCurrency,
                    SponsorshipType = o.SponsorshipType,
                    CategorySubcategory = isDefault ? StringConstants.Default : FormattingHelper.SubcategoryFormatting(o.Subcategory!.Category.Name, o.Subcategory.Name),
                    IsAvailable = available,
                    ActionLink = available
                        ? (isDefault
                            ? this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.CategorySponsor })
                            : this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.CategorySponsor, categoryId = catId }))
                        : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.CategorySponsor, typeId = isDefault ? (int?)null : catId }),
                };
            }).OrderBy(o => o.CategorySubcategory).ThenBy(o => o.Days).ToList();

        private List<SponsoredListingOfferDisplayModel> BuildSubcategoryOfferModels(IEnumerable<SponsoredListingOffer> offers, HashSet<int> freeSubcategories) =>
            offers.Select(o =>
            {
                var isDefault = o.Subcategory == null;
                var subId = o.Subcategory?.SubCategoryId ?? 0;
                var available = isDefault || freeSubcategories.Contains(subId);
                return new SponsoredListingOfferDisplayModel
                {
                    Description = o.Description,
                    Days = o.Days,
                    Price = o.Price,
                    PriceCurrency = o.PriceCurrency,
                    SponsorshipType = o.SponsorshipType,
                    CategorySubcategory = isDefault ? StringConstants.Default : FormattingHelper.SubcategoryFormatting(o.Subcategory!.Category.Name, o.Subcategory.Name),
                    IsAvailable = available,
                    ActionLink = available
                        ? (isDefault
                            ? this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.SubcategorySponsor })
                            : this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.SubcategorySponsor, subCategoryId = subId }))
                        : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.SubcategorySponsor, typeId = isDefault ? (int?)null : subId }),
                };
            }).OrderBy(o => o.CategorySubcategory).ThenBy(o => o.Days).ToList();

        private ActiveSponsoredListingModel ToActiveModel(
            SponsoredListing l,
            string? subcategoryName = null,
            string? categoryName = null) => new ActiveSponsoredListingModel
            {
                ListingName = l.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                SponsoredListingId = l.SponsoredListingId,
                CampaignEndDate = l.CampaignEndDate,
                ListingUrl = l.DirectoryEntry?.Link ?? string.Empty,
                DirectoryListingId = l.DirectoryEntryId,
                SponsorshipType = l.SponsorshipType,
                SubcategoryName = subcategoryName,
                CategoryName = categoryName,
            };

        private string ResolveSubcategoryName(int? subCategoryId)
        {
            if (subCategoryId == null) return string.Empty;
            var sub = this.subCategoryRepository.GetByIdAsync(subCategoryId.Value).Result;
            if (sub == null) return string.Empty;
            var cat = this.categoryRepository.GetByIdAsync(sub.CategoryId).Result;
            return cat == null ? string.Empty : FormattingHelper.SubcategoryFormatting(cat.Name, sub.Name);
        }

        private string ResolveCategoryName(int? categoryId)
        {
            if (categoryId == null) return string.Empty;
            return this.categoryRepository.GetByIdAsync(categoryId.Value).Result?.Name ?? string.Empty;
        }
    }
}