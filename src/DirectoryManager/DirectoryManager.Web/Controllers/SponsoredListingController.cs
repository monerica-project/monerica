using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
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
        private static readonly TimeSpan SponsorJsonTtl = TimeSpan.FromMinutes(20);

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
        private readonly ISponsoredListingOpeningNotificationRepository waitlistRepository;
        private readonly ILogger<SponsoredListingController> logger;

        public SponsoredListingController(
            ISubcategoryRepository subCategoryRepository,
            ICategoryRepository categoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            IMemoryCache cache,
            ISponsoredListingOfferRepository sponsoredListingOfferRepository,
            ISponsoredListingReservationRepository sponsoredListingReservationRepository,
            ICacheService cacheService,
            IDirectoryEntryReviewRepository reviewRepository,
            IUrlResolutionService urlResolutionService,
            ISponsoredListingOpeningNotificationRepository waitlistRepository,
            ILogger<SponsoredListingController> logger)
            : base(cache)
        {
            this.waitlistRepository = waitlistRepository;
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
        // CREATE (admin, no invoice)
        // =====================================================================
        [Route("sponsoredlisting/create")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CreateAsync()
        {
            var entries = (await this.directoryEntryRepository.GetAllowableAdvertisers().ConfigureAwait(false))
                .OrderBy(e => e.Name)
                .ToList();

            var subcategories = (await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false))
                .OrderBy(s => s.Category.Name).ThenBy(s => s.Name)
                .ToList();

            var categories = (await this.categoryRepository.GetAllAsync().ConfigureAwait(false))
                .OrderBy(c => c.Name)
                .ToList();

            this.ViewBag.DirectoryEntries = entries;
            this.ViewBag.Subcategories = subcategories;
            this.ViewBag.Categories = categories;

            var now = DateTime.UtcNow;
            return this.View(new CreateListingViewModel
            {
                CampaignStartDate = now,
                CampaignEndDate = now.AddDays(30),
                SponsorshipType = SponsorshipType.MainSponsor,
            });
        }

        [Route("sponsoredlisting/create")]
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAsync(CreateListingViewModel model)
        {
            if (model.CampaignEndDate <= model.CampaignStartDate)
            {
                this.ModelState.AddModelError(nameof(model.CampaignEndDate), "End date must be after start date.");
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(model.DirectoryEntryId).ConfigureAwait(false);
            if (entry == null)
            {
                this.ModelState.AddModelError(nameof(model.DirectoryEntryId), "Directory entry not found.");
            }

            if (model.SponsorshipType == SponsorshipType.SubcategorySponsor && !model.SubCategoryId.HasValue)
            {
                this.ModelState.AddModelError(nameof(model.SubCategoryId), "Subcategory is required for Subcategory Sponsor.");
            }

            if (model.SponsorshipType == SponsorshipType.CategorySponsor && !model.CategoryId.HasValue)
            {
                this.ModelState.AddModelError(nameof(model.CategoryId), "Category is required for Category Sponsor.");
            }

            if (!this.ModelState.IsValid || entry == null)
            {
                await this.PopulateCreateDropdownsAsync();
                return this.View(model);
            }

            int? subCategoryId = model.SponsorshipType == SponsorshipType.SubcategorySponsor
                ? model.SubCategoryId
                : entry.SubCategoryId;

            int? categoryId = model.SponsorshipType == SponsorshipType.CategorySponsor
                ? model.CategoryId
                : entry.SubCategory?.CategoryId;

            var listing = new SponsoredListing
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                SponsorshipType = model.SponsorshipType,
                CampaignStartDate = DateTime.SpecifyKind(model.CampaignStartDate, DateTimeKind.Utc),
                CampaignEndDate = DateTime.SpecifyKind(model.CampaignEndDate, DateTimeKind.Utc),
                SubCategoryId = subCategoryId,
                CategoryId = categoryId,
                SponsoredListingInvoiceId = 0, // no invoice — admin-created
            };

            var created = await this.sponsoredListingRepository.CreateAsync(listing).ConfigureAwait(false);
            this.ClearCachedItems();

            var admin = this.User?.Identity?.Name ?? "(unknown)";
            this.logger.LogInformation(
                "Sponsored listing {ListingId} created by {Admin} with no invoice (entry {EntryId}, type {Type}, {Start:yyyy-MM-dd} -> {End:yyyy-MM-dd}).",
                created.SponsoredListingId, admin, entry.DirectoryEntryId, model.SponsorshipType,
                listing.CampaignStartDate, listing.CampaignEndDate);

            return this.RedirectToAction(nameof(this.List));
        }

        // =====================================================================
        // DELETE
        // =====================================================================
        [Route("sponsoredlisting/delete/{sponsoredListingId:int}")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> DeleteAsync(int sponsoredListingId)
        {
            var listing = await this.sponsoredListingRepository.GetByIdAsync(sponsoredListingId).ConfigureAwait(false);
            if (listing == null)
            {
                return this.NotFound();
            }

            var entry = await this.directoryEntryRepository.GetByIdAsync(listing.DirectoryEntryId).ConfigureAwait(false);

            return this.View(new EditListingViewModel
            {
                Id = listing.SponsoredListingId,
                CampaignStartDate = listing.CampaignStartDate,
                CampaignEndDate = listing.CampaignEndDate,
                SponsorshipType = listing.SponsorshipType,
                Name = entry?.Name ?? "(unknown)",
                SponsoredListingInvoiceId = listing.SponsoredListingInvoiceId,
            });
        }

        [Route("sponsoredlisting/delete/{sponsoredListingId:int}")]
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmedAsync(int sponsoredListingId)
        {
            var listing = await this.sponsoredListingRepository.GetByIdAsync(sponsoredListingId).ConfigureAwait(false);
            if (listing == null)
            {
                return this.NotFound();
            }

            await this.sponsoredListingRepository.DeleteAsync(sponsoredListingId).ConfigureAwait(false);
            this.ClearCachedItems();

            var admin = this.User?.Identity?.Name ?? "(unknown)";
            this.logger.LogWarning(
                "Sponsored listing {ListingId} deleted by {Admin} (entry {EntryId}, type {Type}, invoice {InvoiceId}).",
                sponsoredListingId, admin, listing.DirectoryEntryId, listing.SponsorshipType, listing.SponsoredListingInvoiceId);

            return this.RedirectToAction(nameof(this.List));
        }

        private async Task PopulateCreateDropdownsAsync()
        {
            this.ViewBag.DirectoryEntries = (await this.directoryEntryRepository.GetAllowableAdvertisers().ConfigureAwait(false))
                .OrderBy(e => e.Name)
                .ToList();

            this.ViewBag.Subcategories = (await this.subCategoryRepository.GetAllActiveSubCategoriesAsync().ConfigureAwait(false))
                .OrderBy(s => s.Category.Name).ThenBy(s => s.Name)
                .ToList();

            this.ViewBag.Categories = (await this.categoryRepository.GetAllAsync().ConfigureAwait(false))
                .OrderBy(c => c.Name)
                .ToList();
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
                CategorySponsorshipOffers = this.BuildCategoryOfferModels(categoryOffers, allCats, freeCategories),
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

            // Resolve each distinct (sub)category name once, asynchronously, before projecting
            // (previously one or two blocking .Result DB calls per sponsor on a public page).
            var subNames = new Dictionary<int, string>();
            foreach (var id in listings
                         .Where(l => l.SponsorshipType == SponsorshipType.SubcategorySponsor)
                         .Select(l => l.SubCategoryId).OfType<int>().Distinct())
            {
                subNames[id] = await this.ResolveSubcategoryNameAsync(id);
            }

            var catNames = new Dictionary<int, string>();
            foreach (var id in listings
                         .Where(l => l.SponsorshipType == SponsorshipType.CategorySponsor)
                         .Select(l => l.CategoryId).OfType<int>().Distinct())
            {
                catNames[id] = await this.ResolveCategoryNameAsync(id);
            }

            var model = new ActiveSponsoredListingViewModel
            {
                MainSponsorItems = listings
                    .Where(l => l.SponsorshipType == SponsorshipType.MainSponsor)
                    .Select(l => this.ToActiveModel(l))
                    .ToList(),
                SubCategorySponsorItems = listings
                    .Where(l => l.SponsorshipType == SponsorshipType.SubcategorySponsor)
                    .Select(l => this.ToActiveModel(l, subcategoryName: l.SubCategoryId is int sid && subNames.TryGetValue(sid, out var sn) ? sn : string.Empty))
                    .ToList(),
                CategorySponsorItems = listings
                    .Where(l => l.SponsorshipType == SponsorshipType.CategorySponsor)
                    .Select(l => this.ToActiveModel(l, categoryName: l.CategoryId is int cid && catNames.TryGetValue(cid, out var cn) ? cn : string.Empty))
                    .ToList(),
            };

            return this.View("activelistings", model);
        }

        [AllowAnonymous]
        [Route("sponsoredlisting/activesponsorjson")]
        [Route("sponsoredlisting/activesponsor.json")]
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
                {
                    return this.Json(sponsorJsonCache);
                }

                sponsorJsonCache = await this.BuildActiveSponsorJsonAsync(ct);
                sponsorJsonCachedAt = DateTimeOffset.UtcNow;
                return this.Json(sponsorJsonCache);
            }
            finally
            {
                SponsorJsonLock.Release();
            }
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
                    WaitlistTypeId = l.SponsorshipType switch
                    {
                        SponsorshipType.CategorySponsor => l.CategoryId,
                        SponsorshipType.SubcategorySponsor => l.SubCategoryId,
                        _ => null,
                    },
                }).ToList(),
            };

            // Waitlist size per placement (one lookup per distinct placement on the page).
            var counts = new Dictionary<(SponsorshipType, int?), int>();
            foreach (var row in model.Listings)
            {
                var key = (row.SponsorshipType, row.WaitlistTypeId);
                if (!counts.TryGetValue(key, out var n))
                {
                    n = await this.waitlistRepository.GetWaitlistCountAsync(key.Item1, key.Item2);
                    counts[key] = n;
                }

                row.WaitlistCount = n;
            }

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
            if (listing == null)
            {
                return this.NotFound();
            }

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
                    ? $"All {max} main sponsor spots are currently taken. Next opening expected around {next.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC."
                    : $"All {max} main sponsor spots are currently taken.";
            }

            var exp = await this.sponsoredListingReservationRepository.GetActiveReservationExpirationAsync(mainGroup);
            if (exp.HasValue)
            {
                var mins = Math.Max(1, (int)Math.Ceiling((exp.Value - DateTime.UtcNow).TotalMinutes));
                return $"A checkout is currently in progress and will expire at {exp.Value.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeShortFormat)} UTC (in {mins} minutes).";
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

                if (sponsor != null)
                {
                    model.UnavailableSubCatetgories.Add(sc.SubCategoryId, label);
                    model.UnavailableSubcategoryExpirations[sc.SubCategoryId] = sponsor.CampaignEndDate;
                }
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

                if (sponsor != null)
                {
                    model.UnavailableCategories.Add(cat.CategoryId, cat.Name);
                    model.UnavailableCategoryExpirations[cat.CategoryId] = sponsor.CampaignEndDate;
                }
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

                // Include the official review in the count so it agrees with the average
                // (which already includes it) — otherwise a sponsor whose only review is the
                // official one reports 0 reviews and its rating is dropped by ad consumers.
                var reviewCount = await this.reviewRepository.CountApprovedRatedIncludingOfficialForEntryAsync(e.DirectoryEntryId, ct);
                var reviewRating = await this.reviewRepository.AverageRatingForEntryApprovedAsync(e.DirectoryEntryId, ct);

                result.Add(new
                {
                    name = e.Name ?? string.Empty,
                    link = e.Link ?? string.Empty,
                    expirationDate = l.CampaignEndDate.ToString(DirectoryManager.Common.Constants.StringConstants.DateTimeFormat),
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

        private List<SponsoredListingOfferDisplayModel> BuildMainOfferModels(IEnumerable<SponsoredListingOffer> offers, int mainActiveCount)
        {
            var available = mainActiveCount < Common.Constants.IntegerConstants.MaxMainSponsoredListings;
            return offers
                .Where(SponsoredListingOfferPlacement.IsStandard)
                .Select(o => new SponsoredListingOfferDisplayModel
                {
                    Description = o.Description,
                    Days = o.Days,
                    Price = o.Price,
                    PriceCurrency = o.PriceCurrency,
                    SponsorshipType = o.SponsorshipType,
                    CategorySubcategory = "Main sponsor (sitewide)",
                    HistoryPlacement = "main",
                    IsAvailable = available,
                    ActionLink = available
                        ? this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.MainSponsor })
                        : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.MainSponsor }),
                })
                .OrderBy(o => o.Days)
                .ToList();
        }

        // One row per category page that can be bought, each with the price it actually costs:
        // its own price, or the standard category price (marked as such) when it has none.
        private List<SponsoredListingOfferDisplayModel> BuildCategoryOfferModels(
            IEnumerable<SponsoredListingOffer> offers, IEnumerable<Category> categories, HashSet<int> freeCategories)
        {
            var list = offers.ToList();
            var standard = list.Where(SponsoredListingOfferPlacement.IsStandard).ToList();
            var own = list
                .Where(o => o.CategoryId.HasValue && o.SubcategoryId == null)
                .GroupBy(o => o.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<SponsoredListingOfferDisplayModel>();
            foreach (var cat in categories.Where(c => c.IsEnabled).OrderBy(c => c.Name))
            {
                var hasOwn = own.TryGetValue(cat.CategoryId, out var catOffers);
                var available = freeCategories.Contains(cat.CategoryId);
                foreach (var o in hasOwn ? catOffers! : standard)
                {
                    rows.Add(new SponsoredListingOfferDisplayModel
                    {
                        Description = o.Description,
                        Days = o.Days,
                        Price = o.Price,
                        PriceCurrency = o.PriceCurrency,
                        SponsorshipType = SponsorshipType.CategorySponsor,
                        CategorySubcategory = cat.Name,
                        IsStandardPrice = !hasOwn,
                        SlotId = cat.CategoryId,
                        HistoryPlacement = $"category-{cat.CategoryId}",
                        IsAvailable = available,
                        ActionLink = available
                            ? this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.CategorySponsor, categoryId = cat.CategoryId })
                            : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.CategorySponsor, typeId = cat.CategoryId }),
                    });
                }
            }

            return rows;
        }

        // The standard subcategory price (every subcategory page without its own price) first, then
        // each subcategory page that has its own price. Retired pages are left out.
        private List<SponsoredListingOfferDisplayModel> BuildSubcategoryOfferModels(IEnumerable<SponsoredListingOffer> offers, HashSet<int> freeSubcategories) =>
            offers
                .Where(SponsoredListingOfferPlacement.IsLivePlacement)
                .Select(o =>
                {
                    var isStandard = SponsoredListingOfferPlacement.IsStandard(o);
                    var subId = o.SubcategoryId ?? 0;
                    var available = isStandard || freeSubcategories.Contains(subId);
                    return new SponsoredListingOfferDisplayModel
                    {
                        Description = o.Description,
                        Days = o.Days,
                        Price = o.Price,
                        PriceCurrency = o.PriceCurrency,
                        SponsorshipType = o.SponsorshipType,
                        CategorySubcategory = isStandard
                            ? StringConstants.Default
                            : FormattingHelper.SubcategoryFormatting(o.Subcategory!.Category?.Name, o.Subcategory.Name),
                        IsStandardPrice = isStandard,
                        SlotId = subId,
                        HistoryPlacement = isStandard ? "subcategory" : $"subcategory-{subId}",
                        IsAvailable = available,
                        ActionLink = available
                            ? (isStandard
                                ? this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.SubcategorySponsor })
                                : this.Url.Action("SelectListing", "SponsoredListingCheckout", new { sponsorshipType = SponsorshipType.SubcategorySponsor, subCategoryId = subId }))
                            : this.Url.Action("Subscribe", "SponsoredListingNotification", new { sponsorshipType = SponsorshipType.SubcategorySponsor, typeId = isStandard ? (int?)null : subId }),
                    };
                })
                .OrderBy(o => o.CategorySubcategory)
                .ThenBy(o => o.Days)
                .ToList();

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

        private async Task<string> ResolveSubcategoryNameAsync(int subCategoryId)
        {
            var sub = await this.subCategoryRepository.GetByIdAsync(subCategoryId);
            if (sub == null)
            {
                return string.Empty;
            }

            var cat = await this.categoryRepository.GetByIdAsync(sub.CategoryId);
            return cat == null ? string.Empty : FormattingHelper.SubcategoryFormatting(cat.Name, sub.Name);
        }

        private async Task<string> ResolveCategoryNameAsync(int categoryId)
        {
            return (await this.categoryRepository.GetByIdAsync(categoryId))?.Name ?? string.Empty;
        }
    }
}