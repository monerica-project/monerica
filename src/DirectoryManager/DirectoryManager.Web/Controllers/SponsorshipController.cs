using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Sponsorship;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [AllowAnonymous]
    [Route("sponsorship")]
    public class SponsorshipController : Controller
    {
        private const int SearchPageSize = 20;
        private const int WaitlistPreviewTake = 10;
        private const int WaitlistPageSize = 25;
        private const int RecentPaidTake = 20;

        private readonly IDirectoryEntryRepository entryRepo;
        private readonly ICategoryRepository categoryRepo;
        private readonly ISubcategoryRepository subcategoryRepo;

        private readonly ISponsoredListingRepository sponsoredListingRepo;
        private readonly ISponsoredListingReservationRepository reservationRepo;
        private readonly ISponsoredListingOfferRepository offerRepo;

        private readonly ISponsoredListingOpeningNotificationRepository waitlistRepo;
        private readonly ISponsoredListingInvoiceRepository invoiceRepo;

        public SponsorshipController(
            IDirectoryEntryRepository entryRepo,
            ICategoryRepository categoryRepo,
            ISubcategoryRepository subcategoryRepo,
            ISponsoredListingRepository sponsoredListingRepo,
            ISponsoredListingReservationRepository reservationRepo,
            ISponsoredListingOfferRepository offerRepo,
            ISponsoredListingOpeningNotificationRepository waitlistRepo,
            ISponsoredListingInvoiceRepository invoiceRepo)
        {
            this.entryRepo = entryRepo;
            this.categoryRepo = categoryRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.sponsoredListingRepo = sponsoredListingRepo;
            this.reservationRepo = reservationRepo;
            this.offerRepo = offerRepo;
            this.waitlistRepo = waitlistRepo;
            this.invoiceRepo = invoiceRepo;
        }


        // GET /sponsorship
        [HttpGet("")]
        public async Task<IActionResult> Index(string? q, int page = 1)
        {
            page = Math.Max(1, page);
            q = (q ?? string.Empty).Trim();

            var vm = new SponsorshipIndexVm
            {
                Query = q,
                Page = page,
                PageSize = SearchPageSize,
                HasSearched = !string.IsNullOrWhiteSpace(q),
            };

            if (vm.HasSearched)
            {
                // Real search (includes tags/category/subcategory/etc)
                var result = await this.entryRepo.SearchAsync(q, page, SearchPageSize);

                vm.TotalCount = result.TotalCount;
                vm.TotalPages = (int)Math.Ceiling(result.TotalCount / (double)SearchPageSize);

                // Use your existing mapper in this controller
                vm.Results = result.Items.Select(this.ToSearchItem).ToList();
            }

            // Waitlist heat (preview on this page)
            vm.WaitlistBoard = await this.BuildWaitlistBoardAsync();

            // Force newest-first (your DTO uses CreateDateUtc -> JoinedUtc in VM)
            if (vm.WaitlistBoard?.MainPreview != null)
            {
                vm.WaitlistBoard.MainPreview = vm.WaitlistBoard.MainPreview
                    .OrderByDescending(x => x.JoinedUtc)
                    .ToList();
            }

            // Recent paid
            vm.RecentPaid = await this.BuildRecentPaidAsync();

            return this.View("Index", vm);
        }


        // POST /sponsorship/select
        [HttpPost("select")]
        [ValidateAntiForgeryToken]
        public IActionResult Select([FromForm] int directoryEntryId)
        {
            return this.RedirectToAction("Options", new { directoryEntryId });
        }

        // GET /sponsorship/options/123
        [HttpGet("options/{directoryEntryId:int}")]
        public async Task<IActionResult> Options(int directoryEntryId, [FromQuery] int subscribed = 0)
        {
            var entry = await this.entryRepo.GetByIdAsync(directoryEntryId);
            if (entry == null)
            {
                return this.NotFound();
            }

            var catId = entry.SubCategory?.CategoryId ?? 0;
            var subId = entry.SubCategoryId;

            var (canAdvertise, reasons) = GetAdvertiseEligibility(entry);

            // Build option panels with availability (but checkout remains in your existing SponsoredListingController)
            var main = await this.BuildTypeOptionAsync(entry, SponsorshipType.MainSponsor, typeIdForScope: null, "Main Sponsor (site-wide)", includeMainSubcategoryCap: true);
            var cat = await this.BuildTypeOptionAsync(entry, SponsorshipType.CategorySponsor, typeIdForScope: catId <= 0 ? null : catId, $"Category Sponsor ({entry.SubCategory?.Category?.Name ?? "Unknown"})");
            var sub = await this.BuildTypeOptionAsync(entry, SponsorshipType.SubcategorySponsor, typeIdForScope: subId <= 0 ? null : subId, $"Subcategory Sponsor ({FormattingHelper.SubcategoryFormatting(entry.SubCategory?.Category?.Name, entry.SubCategory?.Name)})");

            // Price offers (transparent)
            main.Offers = await this.LoadOffersAsync(SponsorshipType.MainSponsor, entry.SubCategoryId);
            cat.Offers = await this.LoadOffersAsync(SponsorshipType.CategorySponsor, null);
            sub.Offers = await this.LoadOffersAsync(SponsorshipType.SubcategorySponsor, entry.SubCategoryId);

            // Scoped waitlist previews (jealousy + competition)
            main.Waitlist = await this.BuildWaitlistPanelAsync(SponsorshipType.MainSponsor, typeId: null);
            cat.Waitlist = catId > 0 ? await this.BuildWaitlistPanelAsync(SponsorshipType.CategorySponsor, typeId: catId) : WaitlistPanelVm.Empty("Category waitlist unavailable (missing category).");
            sub.Waitlist = subId > 0 ? await this.BuildWaitlistPanelAsync(SponsorshipType.SubcategorySponsor, typeId: subId) : WaitlistPanelVm.Empty("Subcategory waitlist unavailable (missing subcategory).");

            var vm = new SponsorshipOptionsVm
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                ListingName = entry.Name ?? StringConstants.DefaultName,
                ListingUrl = entry.Link ?? string.Empty,
                DirectoryEntryKey = entry.DirectoryEntryKey ?? string.Empty,
                DirectoryStatus = entry.DirectoryStatus.ToString(),

                CategoryId = catId,
                SubCategoryId = subId,
                CategoryName = entry.SubCategory?.Category?.Name ?? string.Empty,
                SubcategoryName = entry.SubCategory?.Name ?? string.Empty,

                CanAdvertise = canAdvertise,
                IneligibilityReasons = reasons,
                ShowSubscribedBanner = subscribed == 1,

                Main = main,
                Category = cat,
                Subcategory = sub,

                // show global heat under the wizard too
                WaitlistBoard = await this.BuildWaitlistBoardAsync(),
                RecentPaid = await this.BuildRecentPaidAsync(),
            };

            return this.View("Options", vm);
        }

        // GET /sponsorship/waitlist?type=MainSponsor&typeId=...&page=1
        [HttpGet("waitlist")]
        public async Task<IActionResult> Waitlist(
            [FromQuery] SponsorshipType type = SponsorshipType.MainSponsor,
            [FromQuery] int? typeId = null,
            [FromQuery] int page = 1)
        {
            page = Math.Max(1, page);

            // Validate: MainSponsor uses null typeId
            if (type == SponsorshipType.MainSponsor)
            {
                typeId = null;
            }

            var scopeLabel = await this.GetScopeLabelAsync(type, typeId);
            var paged = await this.waitlistRepo.GetWaitlistPagedAsync(type, typeId, page, WaitlistPageSize);

            // Resolve listing name/url from DirectoryEntryId via repo
            var items = await this.BuildPublicWaitlistItemsAsync(paged.Items);

            var vm = new SponsorshipWaitlistVm
            {
                SponsorshipType = type,
                TypeId = typeId,
                ScopeLabel = scopeLabel,
                TotalCount = paged.TotalCount,
                Items = items
            };

            return this.View("Waitlist", vm);
        }

        // POST /sponsorship/subscribe
        [HttpPost("subscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe([FromForm] SponsorshipSubscribeVm vm)
        {
            var entry = await this.entryRepo.GetByIdAsync(vm.DirectoryEntryId);
            if (entry == null)
            {
                return this.NotFound();
            }

            var email = (vm.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                // you can also ModelState error; keeping it frictionless
                return this.RedirectToAction("Options", new { directoryEntryId = vm.DirectoryEntryId });
            }

            var catId = entry.SubCategory?.CategoryId;
            var subId = entry.SubCategoryId;

            // Upsert selections (listing-scoped to enable "buy for your listing" links later)
            if (vm.NotifyMain)
            {
                await this.waitlistRepo.UpsertAsync(email, SponsorshipType.MainSponsor, typeId: null, directoryEntryId: entry.DirectoryEntryId);
            }

            if (vm.NotifyCategory && catId.HasValue && catId.Value > 0)
            {
                await this.waitlistRepo.UpsertAsync(email, SponsorshipType.CategorySponsor, typeId: catId.Value, directoryEntryId: entry.DirectoryEntryId);
            }

            if (vm.NotifySubcategory && subId > 0)
            {
                await this.waitlistRepo.UpsertAsync(email, SponsorshipType.SubcategorySponsor, typeId: subId, directoryEntryId: entry.DirectoryEntryId);
            }

            return this.RedirectToAction("Options", new { directoryEntryId = entry.DirectoryEntryId, subscribed = 1 });
        }

        // ----------------------------
        // VIEWMODEL BUILDERS
        // ----------------------------
 
        private async Task<RecentPaidVm> BuildRecentPaidAsync()
        {
            var rows = await this.invoiceRepo.GetRecentPaidPurchasesAsync(RecentPaidTake);

            return new RecentPaidVm
            {
                Items = rows.Select(r => new RecentPaidItemVm
                {
                    PaidUtc = r.PaidDateUtc,
                    SponsorshipType = r.SponsorshipType.ToString(),

                    Days = r.Days,
                    AmountUsd = r.AmountUsd,
                    PricePerDayUsd = r.PricePerDayUsd,

                    PaidCurrency = r.PaidCurrency.ToString(),
                    PaidAmount = r.PaidAmount,

                    ExpiresUtc = r.ExpiresUtc,

                    ListingName = r.ListingName,
                    ListingUrl = r.ListingUrl
                }).ToList()
            };
        }



        private SponsorshipSearchItemVm ToSearchItem(DirectoryEntry e)
        {
            var (ok, reasons) = GetAdvertiseEligibility(e);

            var cat = e.SubCategory?.Category?.Name ?? "";
            var sub = e.SubCategory?.Name ?? "";
            var ageDays = (e.CreateDate == DateTime.MinValue) ? 0 : (int)Math.Floor((DateTime.UtcNow - e.CreateDate).TotalDays);

            return new SponsorshipSearchItemVm
            {
                DirectoryEntryId = e.DirectoryEntryId,
                Name = e.Name ?? StringConstants.DefaultName,
                Link = e.Link ?? "",
                DirectoryEntryKey = e.DirectoryEntryKey ?? "",
                Status = e.DirectoryStatus.ToString(),
                AgeDays = ageDays,
                Category = cat,
                Subcategory = FormattingHelper.SubcategoryFormatting(cat, sub),
                CanAdvertise = ok,
                Reasons = reasons,
            };
        }

        private static (bool canAdvertise, List<string> reasons) GetAdvertiseEligibility(DirectoryEntry e)
        {
            var reasons = new List<string>();

            // Allowed advertising statuses: Admitted/Verified ONLY
            if (e.DirectoryStatus != DirectoryStatus.Admitted && e.DirectoryStatus != DirectoryStatus.Verified)
            {
                reasons.Add($"Status is {e.DirectoryStatus}. Must be Admitted or Verified to advertise.");
            }

            // explicit block statuses
            if (e.DirectoryStatus == DirectoryStatus.Questionable || e.DirectoryStatus == DirectoryStatus.Scam)
            {
                reasons.Add("Listing is marked Questionable/Scam and cannot advertise.");
            }

            // Age gate: verified bypasses
            if (e.DirectoryStatus != DirectoryStatus.Verified)
            {
                if (e.CreateDate == DateTime.MinValue)
                {
                    reasons.Add("Listing age is unknown (missing create date).");
                }
                else
                {
                    var days = (int)Math.Floor((DateTime.UtcNow - e.CreateDate).TotalDays);
                    if (days < IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising)
                    {
                        reasons.Add($"Listing is too new: {days} days listed. Needs {IntegerConstants.UnverifiedMinimumDaysListedBeforeAdvertising} days (unless Verified).");
                    }
                }
            }

            return (reasons.Count == 0, reasons);
        }

        private async Task<List<SponsorshipOfferVm>> LoadOffersAsync(SponsorshipType type, int? subcategoryId)
        {
            var offers = await this.offerRepo.GetByTypeAndSubCategoryAsync(type, subcategoryId);
            if (offers == null || !offers.Any())
            {
                offers = await this.offerRepo.GetByTypeAndSubCategoryAsync(type, null);
            }

            return offers
                .OrderBy(x => x.Days)
                .Select(o => new SponsorshipOfferVm
                {
                    Days = o.Days,
                    PriceUsd = o.Price,
                    Description = o.Description ?? "",
                    PricePerDay = o.Days <= 0 ? 0 : Math.Round(o.Price / o.Days, 2),
                })
                .ToList();
        }

        private async Task<SponsorshipTypeOptionVm> BuildTypeOptionAsync(
       DirectoryEntry entry,
       SponsorshipType type,
       int? typeIdForScope,
       string scopeLabel,
       bool includeMainSubcategoryCap = false)
        {
            // Extension allowed regardless of availability
            var isExtension = await this.sponsoredListingRepo.IsSponsoredListingActive(entry.DirectoryEntryId, type);

            // reservation group rules
            var groupTypeId = type switch
            {
                SponsorshipType.MainSponsor => 0,
                SponsorshipType.CategorySponsor => typeIdForScope ?? 0,
                SponsorshipType.SubcategorySponsor => typeIdForScope ?? 0,
                _ => 0
            };
            var group = ReservationGroupHelper.BuildReservationGroupName(type, groupTypeId);

            // capacity scope rules
            int? capacityTypeId = type == SponsorshipType.MainSponsor ? (int?)null : typeIdForScope;

            var active = await this.sponsoredListingRepo.GetActiveSponsorsCountAsync(type, capacityTypeId);
            var reservations = await this.reservationRepo.GetActiveReservationsCountAsync(group);

            var maxSlots = type switch
            {
                SponsorshipType.MainSponsor => Common.Constants.IntegerConstants.MaxMainSponsoredListings,
                SponsorshipType.CategorySponsor => Common.Constants.IntegerConstants.MaxCategorySponsoredListings,
                SponsorshipType.SubcategorySponsor => Common.Constants.IntegerConstants.MaxSubcategorySponsoredListings,
                _ => 0
            };

            // pool-level check
            var poolAvailable = (active < maxSlots) && (reservations < (maxSlots - active));

            // optional: your main-per-subcategory cap (avoid surprise later)
            bool blockedByMainSubCap = false;
            DateTime? nextMainSubCapOpeningUtc = null;

            if (includeMainSubcategoryCap && type == SponsorshipType.MainSponsor && !isExtension)
            {
                var allActiveMain = await this.sponsoredListingRepo.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);

                var activeInSameSub = allActiveMain.Count(x =>
                    (x.SubCategoryId.HasValue && x.SubCategoryId.Value == entry.SubCategoryId) ||
                    (x.SubCategoryId == null && x.DirectoryEntry != null && x.DirectoryEntry.SubCategoryId == entry.SubCategoryId));

                if (activeInSameSub >= Common.Constants.IntegerConstants.MaxMainSponsorsPerSubcategory)
                {
                    blockedByMainSubCap = true;

                    nextMainSubCapOpeningUtc = allActiveMain
                        .Where(x =>
                            (((x.SubCategoryId.HasValue && x.SubCategoryId.Value == entry.SubCategoryId) ||
                              (x.SubCategoryId == null && x.DirectoryEntry != null && x.DirectoryEntry.SubCategoryId == entry.SubCategoryId))
                             && x.CampaignEndDate > DateTime.UtcNow))
                        .OrderBy(x => x.CampaignEndDate)
                        .Select(x => (DateTime?)x.CampaignEndDate)
                        .FirstOrDefault();
                }
            }

            var isAvailableNow = isExtension || (poolAvailable && !blockedByMainSubCap);

            // ----------------------------
            // NEW: who holds the slots now?
            // ----------------------------
            var allActiveForType = await this.sponsoredListingRepo.GetActiveSponsorsByTypeAsync(type);

            var inScope = FilterActiveSponsorsToScope(allActiveForType, type, typeIdForScope);

            // show next-to-expire first (more useful)
            var ordered = inScope
                .Where(x => x.CampaignEndDate > DateTime.UtcNow)
                .OrderBy(x => x.CampaignEndDate)
                .Take(maxSlots)
                .ToList();

            var activeSlots = ordered.Select(x => new ActiveSponsorSlotVm
            {
                DirectoryEntryId = x.DirectoryEntryId,
                ListingName = !string.IsNullOrWhiteSpace(x.DirectoryEntry?.Name) ? x.DirectoryEntry!.Name! : "Listing",
                ListingUrl = x.DirectoryEntry?.Link ?? "",
                CampaignEndUtc = x.CampaignEndDate,
                IsYou = x.DirectoryEntryId == entry.DirectoryEntryId
            }).ToList();

            var yourUntil = ordered
                .Where(x => x.DirectoryEntryId == entry.DirectoryEntryId)
                .Select(x => (DateTime?)x.CampaignEndDate)
                .FirstOrDefault();

            return new SponsorshipTypeOptionVm
            {
                SponsorshipType = type,
                ScopeLabel = scopeLabel,
                IsExtension = isExtension,
                IsAvailableNow = isAvailableNow,

                PoolActiveCount = active,
                PoolMaxSlots = maxSlots,
                PoolHasCheckoutLock = !isExtension && !poolAvailable && active < maxSlots,

                BlockedByMainSubcategoryCap = blockedByMainSubCap,
                NextOpeningForMainSubcategoryCapUtc = nextMainSubCapOpeningUtc,

                // NEW:
                ActiveSlots = activeSlots,
                YourActiveUntilUtc = yourUntil
            };
        }

        private static IEnumerable<SponsoredListing> FilterActiveSponsorsToScope(
            IEnumerable<SponsoredListing> allActive,
            SponsorshipType type,
            int? typeIdForScope)
        {
            var list = allActive ?? Enumerable.Empty<SponsoredListing>();

            if (type == SponsorshipType.MainSponsor)
            {
                return list;
            }

            if (!typeIdForScope.HasValue || typeIdForScope.Value <= 0)
            {
                return Enumerable.Empty<SponsoredListing>();
            }

            var scopeId = typeIdForScope.Value;

            return type switch
            {
                SponsorshipType.CategorySponsor =>
                    list.Where(x =>
                        (x.CategoryId.HasValue && x.CategoryId.Value == scopeId) ||
                        (x.DirectoryEntry != null &&
                         x.DirectoryEntry.SubCategory != null &&
                         x.DirectoryEntry.SubCategory.CategoryId == scopeId)),

                SponsorshipType.SubcategorySponsor =>
                    list.Where(x =>
                        (x.SubCategoryId.HasValue && x.SubCategoryId.Value == scopeId) ||
                        (x.DirectoryEntry != null && x.DirectoryEntry.SubCategoryId == scopeId)),

                _ => Enumerable.Empty<SponsoredListing>()
            };
        }

        private async Task<WaitlistPanelVm> BuildWaitlistPanelAsync(SponsorshipType type, int? typeId)
        {
            // MainSponsor ignores typeId
            if (type == SponsorshipType.MainSponsor)
            {
                typeId = null;
            }

            var scopeLabel = await this.GetScopeLabelAsync(type, typeId);
            var count = await this.waitlistRepo.GetWaitlistCountAsync(type, typeId);
            var preview = await this.waitlistRepo.GetWaitlistPreviewAsync(type, typeId, WaitlistPreviewTake);

            var publicRows = await this.BuildPublicWaitlistRowsAsync(preview);

            return new WaitlistPanelVm
            {
                ScopeLabel = scopeLabel,
                Count = count,
                Preview = publicRows,
                BrowseUrl = this.Url.Action("Waitlist", "Sponsorship", new { type, typeId }),
                JoinWouldBeRank = count 
            };
        }

        private async Task<string> GetScopeLabelAsync(SponsorshipType type, int? typeId)
        {
            if (type == SponsorshipType.MainSponsor)
            {
                return "Main Sponsor (site-wide)";
            }

            if (type == SponsorshipType.CategorySponsor)
            {
                if (typeId.HasValue && typeId.Value > 0)
                {
                    var cat = await this.categoryRepo.GetByIdAsync(typeId.Value);
                    return cat != null ? $"Category Sponsor: {cat.Name}" : "Category Sponsor";
                }
                return "Category Sponsor";
            }

            if (type == SponsorshipType.SubcategorySponsor)
            {
                if (typeId.HasValue && typeId.Value > 0)
                {
                    var sub = await this.subcategoryRepo.GetByIdAsync(typeId.Value);
                    if (sub != null)
                    {
                        var cat = sub.Category ?? await this.categoryRepo.GetByIdAsync(sub.CategoryId);
                        return $"Subcategory Sponsor: {FormattingHelper.SubcategoryFormatting(cat?.Name, sub.Name)}";
                    }
                }
                return "Subcategory Sponsor";
            }

            return "Sponsorship";
        }

        // ----------------------------
        // WAITLIST NAME/URL RESOLUTION
        // ----------------------------

        private async Task<List<WaitlistPublicRowVm>> BuildPublicWaitlistRowsAsync(IEnumerable<WaitlistItemDto> dtos)
        {
            var list = (dtos ?? Enumerable.Empty<WaitlistItemDto>()).ToList();
            var lookup = await this.LoadDirectoryEntryLookupAsync(list.Select(x => x.DirectoryEntryId));

            return list.Select(dto =>
            {
                var entry = TryGetEntry(lookup, dto.DirectoryEntryId);

                return new WaitlistPublicRowVm
                {
                    ListingName = GetListingName(entry, dto.DirectoryEntryId),
                    ListingUrl = entry?.Link ?? string.Empty,
                    JoinedUtc = dto.CreateDateUtc
                };
            }).ToList();
        }

        private async Task<List<WaitlistPublicItemVm>> BuildPublicWaitlistItemsAsync(IEnumerable<WaitlistItemDto> dtos)
        {
            var list = (dtos ?? Enumerable.Empty<WaitlistItemDto>()).ToList();
            var lookup = await this.LoadDirectoryEntryLookupAsync(list.Select(x => x.DirectoryEntryId));

            return list.Select(dto =>
            {
                var entry = TryGetEntry(lookup, dto.DirectoryEntryId);

                return new WaitlistPublicItemVm
                {
                    ListingName = GetListingName(entry, dto.DirectoryEntryId),
                    ListingUrl = entry?.Link ?? string.Empty,
                    JoinedUtc = dto.CreateDateUtc
                };
            }).ToList();
        }

        private async Task<Dictionary<int, DirectoryEntry>> LoadDirectoryEntryLookupAsync(IEnumerable<int?> directoryEntryIds)
        {
            var ids = (directoryEntryIds ?? Enumerable.Empty<int?>())
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var dict = new Dictionary<int, DirectoryEntry>();

            // Bounded (preview/page sizes), so this is fine. If you add a batch method later, swap this.
            foreach (var id in ids)
            {
                var entry = await this.entryRepo.GetByIdAsync(id);
                if (entry != null)
                {
                    dict[id] = entry;
                }
            }

            return dict;
        }

        private static DirectoryEntry? TryGetEntry(Dictionary<int, DirectoryEntry> lookup, int? id)
        {
            if (id.HasValue && id.Value > 0 && lookup.TryGetValue(id.Value, out var e))
            {
                return e;
            }
            return null;
        }

        private static string GetListingName(DirectoryEntry? entry, int? directoryEntryId)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Name))
            {
                return entry.Name;
            }

            // If we have an ID but couldn't load it (removed/hidden), be explicit.
            if (directoryEntryId.HasValue && directoryEntryId.Value > 0)
            {
                return "Listing unavailable";
            }

            return "Anonymous listing";
        }
        private async Task<List<WaitlistPreviewRowVm>> BuildWaitlistPreviewRowsAsync(IEnumerable<WaitlistItemDto> dtos)
        {
            var list = (dtos ?? Enumerable.Empty<WaitlistItemDto>()).ToList();

            // Newest first (per your requirement)
            list = list
                .OrderByDescending(x => x.CreateDateUtc)
                .ThenByDescending(x => x.SponsoredListingOpeningNotificationId)
                .ToList();

            var lookup = await this.LoadDirectoryEntryLookupAsync(list.Select(x => x.DirectoryEntryId));

            return list.Select(dto =>
            {
                var entry = TryGetEntry(lookup, dto.DirectoryEntryId);

                return new WaitlistPreviewRowVm
                {
                    ListingName = GetListingName(entry, dto.DirectoryEntryId),
                    ListingUrl = entry?.Link ?? string.Empty,
                    JoinedUtc = dto.CreateDateUtc
                };
            }).ToList();
        }

        private async Task<WaitlistBoardVm> BuildWaitlistBoardAsync()
        {
            var mainCount = await this.waitlistRepo.GetWaitlistCountAsync(SponsorshipType.MainSponsor, null);

            // IMPORTANT: for newest-first preview, you want the repo to order DESC or sort here.
            var mainPreview = await this.waitlistRepo.GetWaitlistPreviewAsync(
                SponsorshipType.MainSponsor, null, WaitlistPreviewTake);

            var previewRows = await this.BuildWaitlistPreviewRowsAsync(mainPreview);

            return new WaitlistBoardVm
            {
                MainWaitlistCount = mainCount,
                MainPreview = previewRows,
                BrowseWaitlistUrl = this.Url.Action("Waitlist", "Sponsorship", new { type = SponsorshipType.MainSponsor })
            };

        }

    }
}
