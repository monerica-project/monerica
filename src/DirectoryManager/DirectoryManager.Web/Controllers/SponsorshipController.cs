using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Charting;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Models.Sponsorship;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using CommonConstants =
    DirectoryManager.Common.Constants.IntegerConstants;
using SponsorshipPricingSummaryVm = DirectoryManager.Web.Models.Sponsorship.SponsorshipPricingSummaryVm;

namespace DirectoryManager.Web.Controllers
{
    [AllowAnonymous]
    [Route("sponsorship")]
    public class SponsorshipController : Controller
    {
        private const int SearchPageSize = 20;
        private const int WaitlistPreviewTake = 10;
        private const int WaitlistPageSize = 25;
        private const int RecentPaidTake = 10;

        // The public (non-authenticated) pricing charts only ever look back this many days.
        private const int PublicPricingWindowDays = 180;

        private readonly IDirectoryEntryRepository entryRepo;
        private readonly ICategoryRepository categoryRepo;
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly ISponsoredListingRepository sponsoredListingRepo;
        private readonly ISponsoredListingReservationRepository
            reservationRepo;
        private readonly ISponsoredListingOfferRepository offerRepo;
        private readonly ISponsoredListingOpeningNotificationRepository
            waitlistRepo;
        private readonly ISponsoredListingInvoiceRepository invoiceRepo;
        private readonly IUrlResolutionService urlResolver;

        public SponsorshipController(
            IDirectoryEntryRepository entryRepo,
            ICategoryRepository categoryRepo,
            ISubcategoryRepository subcategoryRepo,
            ISponsoredListingRepository sponsoredListingRepo,
            ISponsoredListingReservationRepository reservationRepo,
            ISponsoredListingOfferRepository offerRepo,
            ISponsoredListingOpeningNotificationRepository waitlistRepo,
            ISponsoredListingInvoiceRepository invoiceRepo,
            IUrlResolutionService urlResolver)
        {
            this.entryRepo = entryRepo;
            this.categoryRepo = categoryRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.sponsoredListingRepo = sponsoredListingRepo;
            this.reservationRepo = reservationRepo;
            this.offerRepo = offerRepo;
            this.waitlistRepo = waitlistRepo;
            this.invoiceRepo = invoiceRepo;
            this.urlResolver = urlResolver;
        }

        [HttpGet("")]
        [HttpGet("/waitlist")]
        public async Task<IActionResult> Index(
            string? q, int page = 1)
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
                var result = await this.entryRepo
                    .SearchAsync(q, page, SearchPageSize);

                vm.TotalCount = result.TotalCount;
                vm.TotalPages = ComputePageCount(
                    result.TotalCount, SearchPageSize);
                var activelySponsored = await this.sponsoredListingRepo.GetActivelySponsoredDirectoryEntryIdsAsync();
                vm.Results = result.Items
                    .Select(e => this.ToSearchItem(e, activelySponsored)).ToList();
            }

            vm.WaitlistBoard = await this.BuildWaitlistBoardAsync();
            SortWaitlistPreviewDescending(vm.WaitlistBoard);
            vm.RecentPaid = await this.BuildRecentPaidAsync();
            await this.PopulateMainSponsorInventoryAsync(vm);
            vm.PricingSummaries = await this.BuildPricingSummariesAsync();
            vm.CurrentUtc = DateTime.UtcNow;
            vm.CurrentSponsors = await this.BuildCurrentSponsorsAsync();

            return this.View("Index", vm);
        }

        // Public pricing-trends chart for the sponsorship pages: the average per-day price paid for a
        // slot over the last 6 months (same look as the admin report). With no sponsorshipType it
        // overlays all placements; with one it shows just that placement (e.g. main-only).
        [HttpGet("pricing-trends-chart")]
        public async Task<IActionResult> PricingTrendsChart(SponsorshipType? sponsorshipType = null, bool showCurrentMarket = true)
        {
            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-PublicPricingWindowDays);

            var invoices = await this.invoiceRepo.GetAllAsync().ConfigureAwait(false);
            var paid = invoices.Where(i => i.PaymentStatus == PaymentStatus.Paid).ToList();

            var label = sponsorshipType.HasValue && sponsorshipType.Value != SponsorshipType.Unknown
                ? $"{EnumHelper.GetDescription(sponsorshipType.Value)} — last {PublicPricingWindowDays} days"
                : $"Last {PublicPricingWindowDays} days — all placements";

            var type = sponsorshipType == SponsorshipType.Unknown ? null : sponsorshipType;

            // Current market price per day (today's asking price) for each plotted placement, drawn on
            // the chart so buyers see where the market sits now versus the historical average.
            // The dashed "current market price" reference lines are drawn on the single-placement pages
            // (e.g. /sponsorship/main) but suppressed on the all-placements overview (/sponsorship),
            // where three overlaid dashed lines just clutter the chart.
            var currentDaily = new Dictionary<SponsorshipType, decimal>();
            if (showCurrentMarket)
            {
                var typesForCurrent = type.HasValue
                    ? new[] { type.Value }
                    : new[] { SponsorshipType.MainSponsor, SponsorshipType.CategorySponsor, SponsorshipType.SubcategorySponsor };
                foreach (var t in typesForCurrent)
                {
                    var offers = await this.offerRepo.GetByTypeAndSubCategoryAsync(t, null).ConfigureAwait(false);
                    var baseOffer = offers?
                        .OrderBy(o => o.Days)
                        .FirstOrDefault(o => o.Days >= 30)
                        ?? offers?.OrderBy(o => o.Days).FirstOrDefault();
                    if (baseOffer != null && baseOffer.Days > 0)
                    {
                        currentDaily[t] = Math.Round(baseOffer.Price / baseOffer.Days, 2);
                    }
                }
            }

            var bytes = new InvoicePlotting().CreateDailyAvgPriceByTypeChart(
                paid, Currency.USD, start, end, type, label,
                currentMarketDailyByType: currentDaily);

            if (bytes == null || bytes.Length == 0)
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='400' height='100'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='18' fill='black'>Pricing data coming soon</text>
</svg>";
                return this.File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            return this.File(bytes, StringConstants.PngImage);
        }

        // ---- Public pricing-history explorer -------------------------------------------------
        // A public (non-authenticated) page to explore what advertisers have paid: pick a placement
        // type + subcategory and a date range, capped to the last 180 days. Mirrors the admin report
        // but never looks back further than the public window.
        [HttpGet("pricing")]
        public async Task<IActionResult> PricingHistory(
            int? rangeDays, SponsorshipType? sponsorshipType, int? subCategoryId)
        {
            var range = NormalizeRangeDays(rangeDays);
            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-range);

            var type = sponsorshipType == SponsorshipType.Unknown ? null : sponsorshipType;

            var model = new PublicPricingViewModel
            {
                StartDate = start,
                EndDate = end,
                RangeDays = range,
                WindowDays = PublicPricingWindowDays,
                SponsorshipType = type,
                SubCategoryId = subCategoryId,
                RangeOptions = BuildRangeOptions(range),
            };

            model.SponsorshipTypeOptions = new[]
                {
                    SponsorshipType.MainSponsor, SponsorshipType.CategorySponsor, SponsorshipType.SubcategorySponsor,
                }
                .Select(st => new SelectListItem
                {
                    Value = st.ToString(),
                    Text = EnumHelper.GetDescription(st),
                    Selected = type.HasValue && type.Value == st,
                })
                .Prepend(new SelectListItem { Value = string.Empty, Text = "All (compare)", Selected = !type.HasValue })
                .ToList();

            // Subcategory dropdown: only subcategories that have any paid history (by the listing's subcategory).
            var paid = (await this.invoiceRepo.GetAllAsync().ConfigureAwait(false))
                .Where(i => i.PaymentStatus == PaymentStatus.Paid).ToList();
            var entryIds = paid.Select(i => i.DirectoryEntryId).Distinct().ToList();
            var entryLookup = await this.entryRepo.GetByIdsAsync(entryIds).ConfigureAwait(false);
            var payingSubIds = paid
                .Where(i => entryLookup.TryGetValue(i.DirectoryEntryId, out var e) && e != null)
                .Select(i => entryLookup[i.DirectoryEntryId].SubCategoryId)
                .Distinct().ToHashSet();

            var allSubs = await this.subcategoryRepo.GetAllAsync().ConfigureAwait(false);
            var allCats = (await this.categoryRepo.GetAllAsync().ConfigureAwait(false)).ToList();
            var catNames = allCats.ToDictionary(c => c.CategoryId, c => c.Name);
            var enabledCatIds = allCats.Where(c => c.IsEnabled).Select(c => c.CategoryId).ToHashSet();

            // Only offer subcategories that have paid history AND are still enabled (skip hidden /
            // disabled subcategories, and any whose parent category is disabled — their prices no
            // longer matter).
            var payingSubs = allSubs
                .Where(s => payingSubIds.Contains(s.SubCategoryId) && s.IsEnabled && enabledCatIds.Contains(s.CategoryId))
                .Select(s => new
                {
                    s.SubCategoryId,
                    Label = DirectoryManager.Common.Helpers.CategoryFormatter.Format(
                        s.Category?.Name ?? (catNames.TryGetValue(s.CategoryId, out var cn) ? cn : "Unknown"),
                        s.Name),
                })
                .OrderBy(s => s.Label)
                .ToList();

            model.SubCategoryOptions = payingSubs
                .Select(s => new SelectListItem
                {
                    Value = s.SubCategoryId.ToString(),
                    Text = s.Label,
                    Selected = subCategoryId.HasValue && subCategoryId.Value == s.SubCategoryId,
                })
                .Prepend(new SelectListItem { Value = string.Empty, Text = "All subcategories", Selected = !subCategoryId.HasValue })
                .ToList();

            if (subCategoryId.HasValue)
            {
                model.SubCategoryLabel = payingSubs.FirstOrDefault(s => s.SubCategoryId == subCategoryId.Value)?.Label;
            }

            return this.View(model);
        }

        [HttpGet("pricing-history-chart")]
        public async Task<IActionResult> PricingHistoryChart(
            int? rangeDays, SponsorshipType? sponsorshipType, int? subCategoryId)
        {
            var range = NormalizeRangeDays(rangeDays);
            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-range);

            var type = sponsorshipType == SponsorshipType.Unknown ? null : sponsorshipType;

            var paid = (await this.invoiceRepo.GetAllAsync().ConfigureAwait(false))
                .Where(i => i.PaymentStatus == PaymentStatus.Paid).ToList();

            var scopeLabel = "all subcategories";
            if (subCategoryId.HasValue)
            {
                var entryIds = paid.Select(i => i.DirectoryEntryId).Distinct().ToList();
                var entryLookup = await this.entryRepo.GetByIdsAsync(entryIds).ConfigureAwait(false);
                paid = paid
                    .Where(i => entryLookup.TryGetValue(i.DirectoryEntryId, out var e) && e != null && e.SubCategoryId == subCategoryId.Value)
                    .ToList();
                scopeLabel = (await this.subcategoryRepo.GetByIdAsync(subCategoryId.Value).ConfigureAwait(false))?.Name
                    ?? $"subcategory {subCategoryId.Value}";
            }

            var typeLabel = type.HasValue ? EnumHelper.GetDescription(type.Value) : "All placements";
            var label = $"{typeLabel} · {scopeLabel} · {start:MMM d, yyyy}–{end:MMM d, yyyy}";

            var bytes = new InvoicePlotting().CreateDailyAvgPriceByTypeChart(
                paid, Currency.USD, start, end, type, label, width: 1100, height: 460);

            if (bytes == null || bytes.Length == 0)
            {
                const string svg = @"<svg xmlns='http://www.w3.org/2000/svg' width='600' height='120'>
  <rect width='100%' height='100%' fill='white'/>
  <text x='50%' y='50%' dominant-baseline='middle' text-anchor='middle'
        font-family='sans-serif' font-size='18' fill='#555'>No paid data for this selection</text>
</svg>";
                return this.File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
            }

            return this.File(bytes, StringConstants.PngImage);
        }

        // The only look-back windows the public view offers. Never exceeds the 180-day cap.
        private static readonly int[] AllowedRangeDays = { PublicPricingWindowDays, 90, 60, 30 };

        // Snap a requested window to an allowed preset; anything invalid (or too large) → 180 days.
        private static int NormalizeRangeDays(int? rangeDays)
        {
            var value = rangeDays ?? PublicPricingWindowDays;
            return AllowedRangeDays.Contains(value) ? value : PublicPricingWindowDays;
        }

        private static List<SelectListItem> BuildRangeOptions(int selected) =>
            AllowedRangeDays
                .Select(d => new SelectListItem
                {
                    Value = d.ToString(),
                    Text = $"Last {d} days",
                    Selected = d == selected,
                })
                .ToList();

        [HttpGet("/sponsor-options")]
        [HttpGet("sponsorship/lookup")]
        public async Task<IActionResult> Lookup(string? q, int page = 1)
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
                var result = await this.entryRepo
                    .SearchAsync(q, page, SearchPageSize);

                vm.TotalCount = result.TotalCount;
                vm.TotalPages = ComputePageCount(
                    result.TotalCount, SearchPageSize);
                var activelySponsored = await this.sponsoredListingRepo.GetActivelySponsoredDirectoryEntryIdsAsync();
                vm.Results = result.Items
                    .Select(e => this.ToSearchItem(e, activelySponsored)).ToList();
            }

            // Latest payments only (last 10).
            vm.RecentPaid = await this.BuildRecentPaidAsync(RecentPaidTake);

            // Drives the shared "what each package includes" table.
            vm.PricingSummaries = await this.BuildPricingSummariesAsync();

            return this.View("Lookup", vm);
        }

        [HttpGet("/sponsors")]
        public async Task<IActionResult> Sponsors()
        {
            var max = CommonConstants.MaxMainSponsoredListings;
            var active = await this.sponsoredListingRepo
                .GetActiveSponsorsCountAsync(
                    SponsorshipType.MainSponsor, typeId: null);

            var vm = new SponsorsPageVm
            {
                CurrentUtc = DateTime.UtcNow,
                CurrentSponsors = await this.BuildCurrentSponsorsAsync(),

                // Latest payments only (last 10).
                RecentPaid = await this.BuildRecentPaidAsync(RecentPaidTake),
                PricingSummaries = await this.BuildPricingSummariesAsync(),
                MainWaitlistCount = await this.waitlistRepo
                    .GetWaitlistCountAsync(
                        SponsorshipType.MainSponsor, null),
                MaxSlots = max,
                ActiveCount = active,
            };

            if (active >= max)
            {
                var next = await this.GetNextMainExpirationAsync();
                vm.MainSponsorInventory = new ListingInventoryModel
                {
                    NextListingExpiration = next ?? DateTime.UtcNow
                };
            }

            return this.View("Sponsors", vm);
        }

        [HttpPost("select")]
        [ValidateAntiForgeryToken]
        public IActionResult Select([FromForm] int directoryEntryId)
        {
            return this.RedirectToAction(
                "Options", new { directoryEntryId });
        }

        [HttpGet("options/{directoryEntryId:int}")]
        public async Task<IActionResult> Options(
            int directoryEntryId, [FromQuery] int subscribed = 0)
        {
            var entry = await this.entryRepo
                .GetByIdAsync(directoryEntryId);

            if (entry == null)
            {
                return this.NotFound();
            }

            var vm = await this.BuildOptionsVmAsync(
                entry, subscribed);

            return this.View("Options", vm);
        }

        // Dedicated, single-purpose landing for buying ONLY the Main Sponsor slot.
        // Same search + same checkout as the general page, but the copy explains just
        // the Main placement and each search result leads straight into the Main
        // checkout (skipping the 3-placement Options page that confuses buyers).
        [HttpGet("main")]
        [HttpGet("/main-sponsor")]
        public async Task<IActionResult> MainSponsor(string? q, int page = 1)
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
                var result = await this.entryRepo.SearchAsync(q, page, SearchPageSize);
                vm.TotalCount = result.TotalCount;
                vm.TotalPages = ComputePageCount(result.TotalCount, SearchPageSize);
                var activelySponsored = await this.sponsoredListingRepo.GetActivelySponsoredDirectoryEntryIdsAsync();
                vm.Results = result.Items
                    .Select(e => this.ToSearchItem(e, activelySponsored)).ToList();
            }

            await this.PopulateMainSponsorInventoryAsync(vm);
            vm.PricingSummaries = await this.BuildPricingSummariesAsync();
            vm.CurrentUtc = DateTime.UtcNow;

            // Last 10 MAIN sponsor purchases from VERIFIED listings (good standing; excludes scam /
            // questionable), shown below the pricing graph as real proof of what people pay.
            var mainPurchases = await this.invoiceRepo
                .GetRecentPaidByTypeAsync(SponsorshipType.MainSponsor, 10, new[] { DirectoryStatus.Verified })
                .ConfigureAwait(false);
            vm.RecentPaid = new RecentPaidVm
            {
                Items = mainPurchases.Select(r => new RecentPaidItemVm
                {
                    PaidUtc = r.PaidDateUtc,
                    SponsorshipTypeEnum = r.SponsorshipType,
                    SponsorshipType = EnumHelper.GetDescription(r.SponsorshipType),
                    DirectoryEntryId = r.DirectoryEntryId,
                    Days = r.Days,
                    AmountUsd = r.AmountUsd,
                    PricePerDayUsd = r.PricePerDayUsd,
                    PaidCurrency = r.PaidCurrency.ToString(),
                    PaidAmount = r.PaidAmount,
                    ExpiresUtc = r.ExpiresUtc,
                    ListingName = r.ListingName,
                    ListingUrl = r.ListingUrl,
                }).ToList(),
            };

            return this.View("MainSponsor", vm);
        }

        [HttpGet("waitlist")]
        public async Task<IActionResult> Waitlist(
            [FromQuery] SponsorshipType type =
                SponsorshipType.MainSponsor,
            [FromQuery] int? typeId = null,
            [FromQuery] int page = 1)
        {
            if (this.Request.Query.Count == 0)
            {
                var overview =
                    await this.BuildWaitlistsOverviewAsync();
                return this.View("Waitlists", overview);
            }

            var vm = await this.BuildScopedWaitlistVmAsync(
                type, typeId, page);

            return this.View("Waitlist", vm);
        }

        [HttpPost("subscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(
            [FromForm] SponsorshipSubscribeVm vm)
        {
            var entry = await this.entryRepo
                .GetByIdAsync(vm.DirectoryEntryId);

            if (entry == null)
            {
                return this.NotFound();
            }

            var email = (vm.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                return this.RedirectToOptions(
                    vm.DirectoryEntryId);
            }

            var scopes = CollectSubscriptionScopes(vm, entry);
            if (scopes.Count == 0)
            {
                return this.RedirectToOptions(
                    entry.DirectoryEntryId);
            }

            await this.waitlistRepo.UpsertManyAsync(
                email, entry.DirectoryEntryId, scopes);

            return this.RedirectToAction("Options", new
            {
                directoryEntryId = entry.DirectoryEntryId,
                subscribed = 1
            });
        }

        private static int ComputePageCount(
            int totalCount, int pageSize)
        {
            return (int)Math.Ceiling(
                totalCount / (double)pageSize);
        }

        private static int ComputeAgeDays(DateTime createDate)
        {
            return createDate == DateTime.MinValue
                ? 0
                : (int)Math.Floor(
                    (DateTime.UtcNow - createDate).TotalDays);
        }

        private static int GetMaxSlots(SponsorshipType type)
        {
            return type switch
            {
                SponsorshipType.MainSponsor =>
                    CommonConstants.MaxMainSponsoredListings,
                SponsorshipType.CategorySponsor =>
                    CommonConstants.MaxCategorySponsoredListings,
                SponsorshipType.SubcategorySponsor =>
                    CommonConstants
                        .MaxSubcategorySponsoredListings,
                _ => 0
            };
        }

        private static void SortWaitlistPreviewDescending(
            WaitlistBoardVm? board)
        {
            if (board?.MainPreview == null)
            {
                return;
            }

            board.MainPreview = board.MainPreview
                .OrderByDescending(x => x.JoinedUtc)
                .ToList();
        }

        private static DirectoryEntry? TryGetEntry(
            Dictionary<int, DirectoryEntry> lookup, int? id)
        {
            return id is > 0
                && lookup.TryGetValue(id.Value, out var e)
                    ? e
                    : null;
        }

        private static string ResolveListingName(
            DirectoryEntry? entry, int? directoryEntryId)
        {
            if (entry != null
                && !string.IsNullOrWhiteSpace(entry.Name))
            {
                return entry.Name;
            }

            return directoryEntryId is > 0
                ? "Listing unavailable"
                : "Anonymous listing";
        }

        private static (bool CanAdvertise, List<string> Reasons)
            CheckEligibility(DirectoryEntry e, bool grandfathered)
        {
            // Grandfather clause: a current or past sponsor can always sponsor again or
            // extend, even if the listing no longer meets the standard requirements.
            if (grandfathered)
            {
                return (true, new List<string>());
            }

            var reasons = new List<string>();

            // Requirement 1: must be Verified (green checkmark).
            if (e.DirectoryStatus != DirectoryStatus.Verified)
            {
                reasons.Add(
                    $"Status is {e.DirectoryStatus}. " +
                    "Listing must be Verified (green checkmark) to sponsor.");
            }

            // Requirement 2: must have been listed long enough.
            CheckListingAge(e, reasons);

            return (reasons.Count == 0, reasons);
        }

        private static void CheckListingAge(
            DirectoryEntry e, List<string> reasons)
        {
            if (e.CreateDate == DateTime.MinValue)
            {
                reasons.Add(
                    "Listing age is unknown " +
                    "(missing create date).");
                return;
            }

            var required = CommonConstants.MinimumDaysListedBeforeSponsoring;
            var days = ComputeAgeDays(e.CreateDate);

            if (days < required)
            {
                var daysLeft = required - days;
                reasons.Add(
                    $"Listing is too new: {days} days listed. " +
                    $"Needs {daysLeft} more day{(daysLeft == 1 ? string.Empty : "s")} " +
                    $"(must be listed at least {required} days to sponsor).");
            }
        }

        private static List<(SponsorshipType Type, int? TypeId)>
            CollectSubscriptionScopes(
                SponsorshipSubscribeVm vm,
                DirectoryEntry entry)
        {
            var catId = entry.SubCategory?.CategoryId;
            var subId = entry.SubCategoryId;
            var scopes = new List<(SponsorshipType, int?)>();

            if (vm.NotifyMain)
            {
                scopes.Add((SponsorshipType.MainSponsor, null));
            }

            if (vm.NotifyCategory && catId is > 0)
            {
                scopes.Add((
                    SponsorshipType.CategorySponsor, catId.Value));
            }

            if (vm.NotifySubcategory && subId > 0)
            {
                scopes.Add((
                    SponsorshipType.SubcategorySponsor, subId));
            }

            return scopes;
        }

        private static IEnumerable<SponsoredListing>
            FilterActiveSponsorsToScope(
                IEnumerable<SponsoredListing> allActive,
                SponsorshipType type,
                int? typeIdForScope)
        {
            var list = allActive
                ?? Enumerable.Empty<SponsoredListing>();

            if (type == SponsorshipType.MainSponsor)
            {
                return list;
            }

            if (typeIdForScope is not > 0)
            {
                return Enumerable.Empty<SponsoredListing>();
            }

            var id = typeIdForScope.Value;

            return type switch
            {
                SponsorshipType.CategorySponsor =>
                    list.Where(x =>
                        (x.CategoryId.HasValue
                            && x.CategoryId.Value == id)
                        || (x.DirectoryEntry?.SubCategory
                                ?.CategoryId == id)),

                SponsorshipType.SubcategorySponsor =>
                    list.Where(x =>
                        (x.SubCategoryId.HasValue
                            && x.SubCategoryId.Value == id)
                        || (x.DirectoryEntry != null
                            && x.DirectoryEntry.SubCategoryId
                                == id)),

                _ => Enumerable.Empty<SponsoredListing>()
            };
        }

        private static ActiveSponsorSlotVm ToActiveSponsorSlot(
            SponsoredListing x, DirectoryEntry contextEntry)
        {
            return new ActiveSponsorSlotVm
            {
                DirectoryEntryId = x.DirectoryEntryId,
                ListingName =
                    !string.IsNullOrWhiteSpace(
                        x.DirectoryEntry?.Name)
                        ? x.DirectoryEntry!.Name!
                        : "Listing",
                ListingUrl = x.DirectoryEntry?.Link ?? "",
                CampaignEndUtc = x.CampaignEndDate,
                IsYou = x.DirectoryEntryId
                    == contextEntry.DirectoryEntryId
            };
        }

        private static string BuildScopeLabel(
            SponsorshipType type,
            string? categoryName,
            string? subcategoryName)
        {
            var desc = EnumHelper.GetDescription(type);

            return type switch
            {
                SponsorshipType.MainSponsor =>
                    $"{desc} (site-wide)",
                SponsorshipType.CategorySponsor =>
                    $"{desc} ({categoryName ?? "Unknown"})",
                SponsorshipType.SubcategorySponsor =>
                    $"{desc}  ({FormattingHelper.SubcategoryFormatting(categoryName, subcategoryName)})",
                _ => desc
            };
        }

        private static string BuildPlacementUrl(
            SponsorshipType type, DirectoryEntry? entry)
        {
            var catKey =
                entry?.SubCategory?.Category?.CategoryKey;
            var subKey =
                entry?.SubCategory?.SubCategoryKey;

            return type switch
            {
                SponsorshipType.MainSponsor => "/",

                SponsorshipType.CategorySponsor =>
                    !string.IsNullOrWhiteSpace(catKey)
                        ? $"/{catKey}" : "/",

                SponsorshipType.SubcategorySponsor =>
                    BuildSubcategoryPlacementUrl(catKey, subKey),

                _ => "/"
            };
        }

        private static string BuildSubcategoryPlacementUrl(
            string? catKey, string? subKey)
        {
            if (!string.IsNullOrWhiteSpace(catKey)
                && !string.IsNullOrWhiteSpace(subKey))
            {
                return $"/{catKey}/{subKey}";
            }

            return !string.IsNullOrWhiteSpace(subKey)
                ? $"/{subKey}" : "/";
        }

        private IActionResult RedirectToOptions(
            int directoryEntryId)
        {
            return this.RedirectToAction(
                "Options", new { directoryEntryId });
        }

        private SponsorshipSearchItemVm ToSearchItem(
            DirectoryEntry e, HashSet<int> activelySponsored)
        {
            var (ok, reasons) = CheckEligibility(
                e, activelySponsored.Contains(e.DirectoryEntryId));
            var cat = e.SubCategory?.Category?.Name ?? "";
            var sub = e.SubCategory?.Name ?? "";

            return new SponsorshipSearchItemVm
            {
                DirectoryEntryId = e.DirectoryEntryId,
                Name = e.Name ?? StringConstants.DefaultName,
                Link = e.Link ?? "",
                DirectoryEntryKey = e.DirectoryEntryKey ?? "",
                Status = e.DirectoryStatus.ToString(),
                AgeDays = ComputeAgeDays(e.CreateDate),
                Category = cat,
                Subcategory = sub,
                CanAdvertise = ok,
                Reasons = reasons,
            };
        }

        private async Task PopulateMainSponsorInventoryAsync(
            SponsorshipIndexVm vm)
        {
            var max = CommonConstants.MaxMainSponsoredListings;
            var active = await this.sponsoredListingRepo
                .GetActiveSponsorsCountAsync(
                    SponsorshipType.MainSponsor, typeId: null);

            vm.MainSponsorMaxSlots = max;
            vm.MainSponsorActiveCount = active;
            vm.MainSponsorIsOpen = active < max;

            if (!vm.MainSponsorIsOpen)
            {
                var nextExpiration =
                    await this.GetNextMainExpirationAsync();

                vm.MainSponsorInventory = new ListingInventoryModel
                {
                    NextListingExpiration =
                        nextExpiration ?? DateTime.UtcNow
                };
            }
        }

        private async Task<DateTime?> GetNextMainExpirationAsync()
        {
            var now = DateTime.UtcNow;
            var allActive = await this.sponsoredListingRepo
                .GetActiveSponsorsByTypeAsync(
                    SponsorshipType.MainSponsor);

            return allActive
                .Where(x => x.CampaignEndDate > now)
                .OrderBy(x => x.CampaignEndDate)
                .Select(x => (DateTime?)x.CampaignEndDate)
                .FirstOrDefault();
        }

        private async Task<SponsorshipOptionsVm>
            BuildOptionsVmAsync(
                DirectoryEntry entry, int subscribed)
        {
            var catId = entry.SubCategory?.CategoryId ?? 0;
            var subId = entry.SubCategoryId;
            var catName = entry.SubCategory?.Category?.Name;
            var subName = entry.SubCategory?.Name;
            var grandfathered = await this.sponsoredListingRepo
                .HasActiveSponsorshipAsync(entry.DirectoryEntryId);
            var (canAdvertise, reasons) = CheckEligibility(entry, grandfathered);

            var main = await this.BuildTypeOptionAsync(
                entry,
                SponsorshipType.MainSponsor,
                typeIdForScope: null,
                BuildScopeLabel(
                    SponsorshipType.MainSponsor, null, null),
                includeMainSubcategoryCap: true);

            var cat = await this.BuildTypeOptionAsync(
                entry,
                SponsorshipType.CategorySponsor,
                typeIdForScope: catId <= 0 ? null : catId,
                BuildScopeLabel(
                    SponsorshipType.CategorySponsor,
                    catName, null));

            var sub = await this.BuildTypeOptionAsync(
                entry,
                SponsorshipType.SubcategorySponsor,
                typeIdForScope: subId <= 0 ? null : subId,
                BuildScopeLabel(
                    SponsorshipType.SubcategorySponsor,
                    catName, subName));

            int? pricingSubId =
                entry.SubCategoryId > 0
                    ? entry.SubCategoryId : null;

            main.Offers = await this.LoadOffersAsync(
                SponsorshipType.MainSponsor, pricingSubId);
            cat.Offers = await this.LoadOffersAsync(
                SponsorshipType.CategorySponsor, pricingSubId);
            sub.Offers = await this.LoadOffersAsync(
                SponsorshipType.SubcategorySponsor, pricingSubId);

            // Apply the cross-tier existing-sponsor perk to each placement this listing could buy, so the
            // options page shows the same discounted pricing the buyer will get at checkout.
            await this.ApplyExistingSponsorPerkToOptionAsync(entry.DirectoryEntryId, main);
            await this.ApplyExistingSponsorPerkToOptionAsync(entry.DirectoryEntryId, cat);
            await this.ApplyExistingSponsorPerkToOptionAsync(entry.DirectoryEntryId, sub);

            main.Waitlist = await this.BuildWaitlistPanelAsync(
                SponsorshipType.MainSponsor, typeId: null);

            cat.Waitlist = catId > 0
                ? await this.BuildWaitlistPanelAsync(
                    SponsorshipType.CategorySponsor, typeId: catId)
                : WaitlistPanelVm.Empty(
                    "Category waitlist unavailable " +
                    "(missing category).");

            sub.Waitlist = subId > 0
                ? await this.BuildWaitlistPanelAsync(
                    SponsorshipType.SubcategorySponsor,
                    typeId: subId)
                : WaitlistPanelVm.Empty(
                    "Subcategory waitlist unavailable " +
                    "(missing subcategory).");

            return new SponsorshipOptionsVm
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                ListingName =
                    entry.Name ?? StringConstants.DefaultName,
                ListingUrl = entry.Link ?? string.Empty,
                DirectoryEntryKey =
                    entry.DirectoryEntryKey ?? string.Empty,
                DirectoryStatus =
                    entry.DirectoryStatus.ToString(),

                CategoryId = catId,
                SubCategoryId = subId,
                CategoryName = catName ?? string.Empty,
                SubcategoryName = subName ?? string.Empty,

                CanAdvertise = canAdvertise,
                IneligibilityReasons = reasons,
                ShowSubscribedBanner = subscribed == 1,

                Main = main,
                Category = cat,
                Subcategory = sub,

                WaitlistBoard =
                    await this.BuildWaitlistBoardAsync(),
                RecentPaid =
                    await this.BuildRecentPaidAsync(),
            };
        }

        private async Task<List<SponsorshipOfferVm>> LoadOffersAsync(
            SponsorshipType type, int? subcategoryId)
        {
            var offers = await this.offerRepo
                .GetByTypeAndSubCategoryAsync(type, subcategoryId);

            if (offers == null || !offers.Any())
            {
                offers = await this.offerRepo
                    .GetByTypeAndSubCategoryAsync(type, null);
            }

            return offers
                .OrderBy(x => x.Days)
                .Select(o => new SponsorshipOfferVm
                {
                    Days = o.Days,
                    PriceUsd = o.Price,
                    Description = o.Description ?? "",
                    PricePerDay = o.Days <= 0
                        ? 0
                        : Math.Round(o.Price / o.Days, 2),
                })
                .ToList();
        }

        private async Task ApplyExistingSponsorPerkToOptionAsync(
            int directoryEntryId, SponsorshipTypeOptionVm option)
        {
            var (percent, basisTier) = await DirectoryManager.Web.Helpers.SponsorshipDiscountHelper
                .GetCrossTierDiscountAsync(this.sponsoredListingRepo, directoryEntryId, option.SponsorshipType)
                .ConfigureAwait(false);

            option.ExistingSponsorPerkPercent = percent;
            option.ExistingSponsorPerkBasisTier = basisTier;

            foreach (var offer in option.Offers)
            {
                offer.DiscountPercent = percent;
                offer.DiscountedPriceUsd = DirectoryManager.Web.Helpers.SponsorshipDiscountHelper
                    .ApplyPercentDiscount(offer.PriceUsd, percent);
            }
        }

        private async Task<SponsorshipTypeOptionVm>
            BuildTypeOptionAsync(
                DirectoryEntry entry,
                SponsorshipType type,
                int? typeIdForScope,
                string scopeLabel,
                bool includeMainSubcategoryCap = false)
        {
            var isExtension = await this.sponsoredListingRepo
                .IsSponsoredListingActive(
                    entry.DirectoryEntryId, type);

            var poolAvailable = await this.IsPoolAvailableAsync(
                type, typeIdForScope);

            var (blockedBySubCap, nextSubCapUtc) =
                includeMainSubcategoryCap && !isExtension
                    ? await this.CheckMainSubcategoryCapAsync(
                        entry, type)
                    : (false, (DateTime?)null);

            var isAvailableNow =
                isExtension || (poolAvailable && !blockedBySubCap);

            var maxSlots = GetMaxSlots(type);
            var capacityTypeId =
                type == SponsorshipType.MainSponsor
                    ? null : typeIdForScope;
            var active = await this.sponsoredListingRepo
                .GetActiveSponsorsCountAsync(
                    type, capacityTypeId);

            var allActiveForType = await this.sponsoredListingRepo
                .GetActiveSponsorsByTypeAsync(type);
            var inScope = FilterActiveSponsorsToScope(
                allActiveForType, type, typeIdForScope);

            var ordered = inScope
                .Where(x => x.CampaignEndDate > DateTime.UtcNow)
                .OrderBy(x => x.CampaignEndDate)
                .Take(maxSlots)
                .ToList();

            return new SponsorshipTypeOptionVm
            {
                SponsorshipType = type,
                ScopeLabel = scopeLabel,
                IsExtension = isExtension,
                IsAvailableNow = isAvailableNow,

                PoolActiveCount = active,
                PoolMaxSlots = maxSlots,
                PoolHasCheckoutLock =
                    !isExtension
                    && !poolAvailable
                    && active < maxSlots,

                BlockedByMainSubcategoryCap = blockedBySubCap,
                NextOpeningForMainSubcategoryCapUtc =
                    nextSubCapUtc,

                ActiveSlots = ordered
                    .Select(x => ToActiveSponsorSlot(x, entry))
                    .ToList(),
                YourActiveUntilUtc = ordered
                    .Where(x => x.DirectoryEntryId
                        == entry.DirectoryEntryId)
                    .Select(x => (DateTime?)x.CampaignEndDate)
                    .FirstOrDefault()
            };
        }

        private async Task<bool> IsPoolAvailableAsync(
            SponsorshipType type, int? typeIdForScope)
        {
            var capacityTypeId =
                type == SponsorshipType.MainSponsor
                    ? null : typeIdForScope;
            var maxSlots = GetMaxSlots(type);
            var active = await this.sponsoredListingRepo
                .GetActiveSponsorsCountAsync(
                    type, capacityTypeId);

            var groupTypeId = type switch
            {
                SponsorshipType.MainSponsor => 0,
                _ => typeIdForScope ?? 0
            };
            var group = ReservationGroupHelper
                .BuildReservationGroupName(type, groupTypeId);
            var reservations = await this.reservationRepo
                .GetActiveReservationsCountAsync(group);

            return active < maxSlots
                && reservations < (maxSlots - active);
        }

        private async Task<(bool Blocked, DateTime? NextOpening)>
            CheckMainSubcategoryCapAsync(
                DirectoryEntry entry, SponsorshipType type)
        {
            if (type != SponsorshipType.MainSponsor)
            {
                return (false, null);
            }

            var allActiveMain = await this.sponsoredListingRepo
                .GetActiveSponsorsByTypeAsync(
                    SponsorshipType.MainSponsor);

            bool MatchesSub(SponsoredListing x) =>
                (x.SubCategoryId.HasValue
                    && x.SubCategoryId.Value
                        == entry.SubCategoryId)
                || (x.SubCategoryId == null
                    && x.DirectoryEntry != null
                    && x.DirectoryEntry.SubCategoryId
                        == entry.SubCategoryId);

            var count = allActiveMain.Count(MatchesSub);

            if (count
                < CommonConstants.MaxMainSponsorsPerSubcategory)
            {
                return (false, null);
            }

            var nextOpening = allActiveMain
                .Where(x => MatchesSub(x)
                    && x.CampaignEndDate > DateTime.UtcNow)
                .OrderBy(x => x.CampaignEndDate)
                .Select(x => (DateTime?)x.CampaignEndDate)
                .FirstOrDefault();

            return (true, nextOpening);
        }

        private async Task<WaitlistPanelVm>
            BuildWaitlistPanelAsync(
                SponsorshipType type, int? typeId)
        {
            if (type == SponsorshipType.MainSponsor)
            {
                typeId = null;
            }

            var scopeLabel =
                await this.GetScopeLabelAsync(type, typeId);
            var count = await this.waitlistRepo
                .GetWaitlistCountAsync(type, typeId);
            var preview = await this.waitlistRepo
                .GetWaitlistPreviewAsync(
                    type, typeId, WaitlistPreviewTake);

            return new WaitlistPanelVm
            {
                ScopeLabel = scopeLabel,
                Count = count,
                Preview = await this.MapWaitlistToRowsAsync(
                    preview),
                BrowseUrl = this.Url.Action(
                    "Waitlist", "Sponsorship",
                    new { type, typeId }),
                JoinWouldBeRank = count + 1
            };
        }

        private async Task<WaitlistBoardVm>
            BuildWaitlistBoardAsync()
        {
            var mainCount = await this.waitlistRepo
                .GetWaitlistCountAsync(
                    SponsorshipType.MainSponsor, null);
            var mainPreview = await this.waitlistRepo
                .GetWaitlistPreviewAsync(
                    SponsorshipType.MainSponsor,
                    null,
                    WaitlistPreviewTake);

            return new WaitlistBoardVm
            {
                MainWaitlistCount = mainCount,
                MainPreview =
                    await this.MapWaitlistToPreviewRowsAsync(
                        mainPreview),
                BrowseWaitlistUrl =
                    this.Url.Action("Waitlist", "Sponsorship")
            };
        }

        private async Task<SponsorshipWaitlistVm>
            BuildScopedWaitlistVmAsync(
                SponsorshipType type,
                int? typeId,
                int page)
        {
            page = Math.Max(1, page);

            if (type == SponsorshipType.MainSponsor)
            {
                typeId = null;
            }

            var scopeLabel =
                await this.GetScopeLabelAsync(type, typeId);
            var paged = await this.waitlistRepo
                .GetWaitlistPagedAsync(
                    type, typeId, page, WaitlistPageSize);

            return new SponsorshipWaitlistVm
            {
                SponsorshipType = type,
                TypeId = typeId,
                ScopeLabel = scopeLabel,
                TotalCount = paged.TotalCount,
                Items = await this.MapWaitlistToPublicItemsAsync(
                    paged.Items)
            };
        }

        private async Task<SponsorshipWaitlistsOverviewVm>
            BuildWaitlistsOverviewAsync()
        {
            var mainRows = await this.waitlistRepo
                .GetWaitlistAllByTypeAsync(
                    SponsorshipType.MainSponsor);
            var catRows = await this.waitlistRepo
                .GetWaitlistAllByTypeAsync(
                    SponsorshipType.CategorySponsor);
            var subRows = await this.waitlistRepo
                .GetWaitlistAllByTypeAsync(
                    SponsorshipType.SubcategorySponsor);

            var entryLookup =
                await this.LoadDirectoryEntryLookupAsync(
                    mainRows.Concat(catRows).Concat(subRows)
                        .Select(x => x.DirectoryEntryId));

            List<WaitlistPublicItemVm> MapScopedItems(
                IEnumerable<WaitlistScopedItemDto> rows)
            {
                return rows
                    .Select(dto =>
                    {
                        var entry = TryGetEntry(
                            entryLookup, dto.DirectoryEntryId);

                        return new WaitlistPublicItemVm
                        {
                            ListingName = ResolveListingName(
                                entry, dto.DirectoryEntryId),
                            ListingUrl =
                                this.WaitlistProfileUrl(entry),
                            JoinedUtc = dto.SubscribedDateUtc
                        };
                    })
                    .OrderByDescending(x => x.JoinedUtc)
                    .ThenBy(
                        x => x.ListingName,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var mainSection = new SponsorshipWaitlistSectionVm
            {
                SponsorshipType = SponsorshipType.MainSponsor,
                TypeId = null,
                ScopeLabel = await this.GetScopeLabelAsync(
                    SponsorshipType.MainSponsor, null),
                TotalCount = mainRows.Count,
                BrowseUrl = this.Url.Action(
                    "Waitlist", "Sponsorship",
                    new
                    {
                        type = SponsorshipType.MainSponsor
                    }) ?? "",
                Items = MapScopedItems(mainRows)
            };

            var categorySections =
                await this.BuildGroupedSectionsAsync(
                    catRows,
                    SponsorshipType.CategorySponsor,
                    MapScopedItems);

            var subcategorySections =
                await this.BuildGroupedSectionsAsync(
                    subRows,
                    SponsorshipType.SubcategorySponsor,
                    MapScopedItems);

            return new SponsorshipWaitlistsOverviewVm
            {
                TotalCount = mainSection.TotalCount
                    + categorySections.Sum(x => x.TotalCount)
                    + subcategorySections.Sum(x => x.TotalCount),
                Main = mainSection,
                Categories = categorySections,
                Subcategories = subcategorySections
            };
        }

        private async Task<List<SponsorshipWaitlistSectionVm>>
            BuildGroupedSectionsAsync(
                IReadOnlyList<WaitlistScopedItemDto> rows,
                SponsorshipType type,
                Func<IEnumerable<WaitlistScopedItemDto>,
                    List<WaitlistPublicItemVm>> mapItems)
        {
            var sections =
                new List<SponsorshipWaitlistSectionVm>();

            foreach (var g in rows
                .Where(x => x.TypeId is > 0)
                .GroupBy(x => x.TypeId!.Value))
            {
                var label =
                    await this.GetScopeLabelAsync(type, g.Key);

                sections.Add(new SponsorshipWaitlistSectionVm
                {
                    SponsorshipType = type,
                    TypeId = g.Key,
                    ScopeLabel = label,
                    TotalCount = g.Count(),
                    BrowseUrl = this.Url.Action(
                        "Waitlist", "Sponsorship",
                        new { type, typeId = g.Key }) ?? "",
                    Items = mapItems(g)
                });
            }

            return sections
                .OrderBy(
                    x => x.ScopeLabel,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<List<CurrentSponsorItemVm>>
            BuildCurrentSponsorsAsync()
        {
            var now = DateTime.UtcNow;

            var active = await this.sponsoredListingRepo
                .GetAllActiveSponsorsAsync();

            var ordered = active
                .Where(x => x.CampaignEndDate > now)
                .OrderBy(x => x.CampaignEndDate)
                .ThenBy(
                    x => x.DirectoryEntry?.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = new List<CurrentSponsorItemVm>();

            foreach (var x in ordered)
            {
                items.Add(new CurrentSponsorItemVm
                {
                    DirectoryEntryId = x.DirectoryEntryId,
                    ListingName =
                        !string.IsNullOrWhiteSpace(
                            x.DirectoryEntry?.Name)
                            ? x.DirectoryEntry!.Name!
                            : "Listing",
                    ListingUrl = x.DirectoryEntry?.Link ?? string.Empty,
                    SponsorshipTypeEnum = x.SponsorshipType,
                    SponsorshipType =
                        EnumHelper.GetDescription(x.SponsorshipType),
                    ScopeLabel = await this.BuildSponsorScopeNameAsync(x),
                    ExpiresUtc = x.CampaignEndDate,
                    RenewUrl =
                        x.DirectoryEntryId > 0
                        && x.SponsorshipType != SponsorshipType.Unknown
                            ? "/sponsoredlisting/selectduration"
                                + $"?directoryEntryId={x.DirectoryEntryId}"
                                + $"&sponsorshipType={x.SponsorshipType}"
                            : string.Empty,
                });
            }

            return items;
        }

        /// <summary>
        /// The scope a sponsorship covers, with no type prefix: empty for a main
        /// (site-wide) sponsor, the category name for a category sponsor, and
        /// "Category &gt; Subcategory" for a subcategory sponsor.
        /// </summary>
        private async Task<string> BuildSponsorScopeNameAsync(
            SponsoredListing listing)
        {
            switch (listing.SponsorshipType)
            {
                case SponsorshipType.CategorySponsor:
                    var categoryId = listing.CategoryId
                        ?? listing.DirectoryEntry?.SubCategory?.CategoryId;

                    if (categoryId is not > 0)
                    {
                        return string.Empty;
                    }

                    var category = await this.categoryRepo
                        .GetByIdAsync(categoryId.Value);

                    return category?.Name ?? string.Empty;

                case SponsorshipType.SubcategorySponsor:
                    var subcategoryId = listing.SubCategoryId
                        ?? listing.DirectoryEntry?.SubCategoryId;

                    if (subcategoryId is not > 0)
                    {
                        return string.Empty;
                    }

                    var subcategory = await this.subcategoryRepo
                        .GetByIdAsync(subcategoryId.Value);

                    if (subcategory == null)
                    {
                        return string.Empty;
                    }

                    var parent = subcategory.Category
                        ?? await this.categoryRepo
                            .GetByIdAsync(subcategory.CategoryId);

                    return FormattingHelper.SubcategoryFormatting(
                        parent?.Name, subcategory.Name);

                default:
                    return string.Empty;
            }
        }

        private async Task<RecentPaidVm> BuildRecentPaidAsync(
            int take = RecentPaidTake)
        {
            var rows = await this.invoiceRepo
                .GetRecentPaidActivePurchasesAsync(take);

            var entryLookup = await this.entryRepo.GetByIdsAsync(
                rows.Select(x => x.DirectoryEntryId)
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList());

            return new RecentPaidVm
            {
                Items = rows.Select(r =>
                {
                    entryLookup.TryGetValue(
                        r.DirectoryEntryId, out var entry);

                    return new RecentPaidItemVm
                    {
                        PaidUtc = r.PaidDateUtc,
                        SponsorshipTypeEnum = r.SponsorshipType,
                        SponsorshipType = EnumHelper
                            .GetDescription(r.SponsorshipType),
                        DirectoryEntryId = r.DirectoryEntryId,
                        PlacementUrl = BuildPlacementUrl(
                            r.SponsorshipType, entry),
                        Days = r.Days,
                        AmountUsd = r.AmountUsd,
                        PricePerDayUsd = r.PricePerDayUsd,
                        PaidCurrency =
                            r.PaidCurrency.ToString(),
                        PaidAmount = r.PaidAmount,
                        ExpiresUtc = r.ExpiresUtc,
                        ListingName = r.ListingName,
                        ListingUrl = r.ListingUrl
                    };
                }).ToList()
            };
        }

        private async Task<string> GetScopeLabelAsync(
            SponsorshipType type, int? typeId)
        {
            var desc = EnumHelper.GetDescription(type);

            return type switch
            {
                SponsorshipType.MainSponsor =>
                    $"{desc} (site-wide)",

                SponsorshipType.CategorySponsor
                    when typeId is > 0 =>
                    await this.FormatCategoryScopeLabelAsync(
                        desc, typeId.Value),

                SponsorshipType.SubcategorySponsor
                    when typeId is > 0 =>
                    await this.FormatSubcategoryScopeLabelAsync(
                        desc, typeId.Value),

                _ => desc
            };
        }

        private async Task<string>
            FormatCategoryScopeLabelAsync(
                string desc, int categoryId)
        {
            var cat = await this.categoryRepo
                .GetByIdAsync(categoryId);

            return cat != null
                ? $"{desc}: {cat.Name}" : desc;
        }

        private async Task<string>
            FormatSubcategoryScopeLabelAsync(
                string desc, int subcategoryId)
        {
            var sub = await this.subcategoryRepo
                .GetByIdAsync(subcategoryId);

            if (sub == null)
            {
                return desc;
            }

            var cat = sub.Category
                ?? await this.categoryRepo
                    .GetByIdAsync(sub.CategoryId);

            return $"{desc}: " +
                FormattingHelper.SubcategoryFormatting(
                    cat?.Name, sub.Name);
        }

        private async Task<List<SponsorshipPricingSummaryVm>> BuildPricingSummariesAsync()
        {
            var types = new[]
            {
        SponsorshipType.MainSponsor,
        SponsorshipType.CategorySponsor,
        SponsorshipType.SubcategorySponsor,
            };

            var summaries = new List<SponsorshipPricingSummaryVm>();

            foreach (var type in types)
            {
                var offers = await this.offerRepo.GetAllByTypeAsync(type);

                if (offers == null || !offers.Any())
                {
                    continue;
                }

                var validOffers = offers.Where(o => o.Days > 0).ToList();
                if (!validOffers.Any())
                {
                    continue;
                }

                var perDay = validOffers
                    .Select(o => Math.Round(o.Price / o.Days, 2))
                    .ToList();

                summaries.Add(new SponsorshipPricingSummaryVm
                {
                    SponsorshipType = type,
                    Label = EnumHelper.GetDescription(type),
                    MinPriceUsd = validOffers.Min(o => o.Price),
                    MaxPriceUsd = validOffers.Max(o => o.Price),
                    MinDays = validOffers.Min(o => o.Days),
                    MaxDays = validOffers.Max(o => o.Days),
                    MinUsdPerDay = perDay.Min(),
                    MaxUsdPerDay = perDay.Max(),
                });
            }

            return summaries;
        }

        private async Task<List<T>> MapWaitlistDtosAsync<T>(
            IEnumerable<WaitlistItemDto> dtos,
            Func<DirectoryEntry?, WaitlistItemDto, T> mapper)
        {
            var list =
                (dtos ?? Enumerable.Empty<WaitlistItemDto>())
                    .ToList();

            var lookup =
                await this.LoadDirectoryEntryLookupAsync(
                    list.Select(x => x.DirectoryEntryId));

            return list
                .Select(dto => mapper(
                    TryGetEntry(lookup, dto.DirectoryEntryId),
                    dto))
                .ToList();
        }

        // Waitlist rows link to the entry's canonical Monerica profile page
        // (https://monerica.com/site/{key}) rather than its external website or the
        // app.monerica.com host the waitlist itself is served on. The resolver returns
        // the canonical clearnet URL off-Tor, and a relative path on Tor/local so the
        // onion still works. Falls back to the external link only if the key is missing.
        private string WaitlistProfileUrl(DirectoryEntry? entry)
            => entry != null && !string.IsNullOrWhiteSpace(entry.DirectoryEntryKey)
                ? this.urlResolver.ResolveToRoot(UrlBuilder.ListingPath(entry.DirectoryEntryKey))
                : (entry?.Link ?? string.Empty);

        private async Task<List<WaitlistPublicRowVm>>
            MapWaitlistToRowsAsync(
                IEnumerable<WaitlistItemDto> dtos)
        {
            return await this.MapWaitlistDtosAsync(
                dtos,
                (entry, dto) => new WaitlistPublicRowVm
                {
                    ListingName = ResolveListingName(
                        entry, dto.DirectoryEntryId),
                    ListingUrl = this.WaitlistProfileUrl(entry),
                    JoinedUtc = dto.CreateDateUtc
                });
        }

        private async Task<List<WaitlistPublicItemVm>>
            MapWaitlistToPublicItemsAsync(
                IEnumerable<WaitlistItemDto> dtos)
        {
            return await this.MapWaitlistDtosAsync(
                dtos,
                (entry, dto) => new WaitlistPublicItemVm
                {
                    ListingName = ResolveListingName(
                        entry, dto.DirectoryEntryId),
                    ListingUrl = this.WaitlistProfileUrl(entry),
                    JoinedUtc = dto.CreateDateUtc
                });
        }

        private async Task<List<WaitlistPreviewRowVm>>
            MapWaitlistToPreviewRowsAsync(
                IEnumerable<WaitlistItemDto> dtos)
        {
            var list =
                (dtos ?? Enumerable.Empty<WaitlistItemDto>())
                    .OrderByDescending(x => x.CreateDateUtc)
                    .ThenByDescending(
                        x => x
                            .SponsoredListingOpeningNotificationId)
                    .ToList();

            var lookup =
                await this.LoadDirectoryEntryLookupAsync(
                    list.Select(x => x.DirectoryEntryId));

            return list.Select(dto =>
            {
                var entry = TryGetEntry(
                    lookup, dto.DirectoryEntryId);

                return new WaitlistPreviewRowVm
                {
                    ListingName = ResolveListingName(
                        entry, dto.DirectoryEntryId),
                    ListingUrl = this.WaitlistProfileUrl(entry),
                    JoinedUtc = dto.CreateDateUtc
                };
            }).ToList();
        }

        private async Task<Dictionary<int, DirectoryEntry>>
            LoadDirectoryEntryLookupAsync(
                IEnumerable<int?> directoryEntryIds)
        {
            var ids =
                (directoryEntryIds ?? Enumerable.Empty<int?>())
                    .Where(x => x is > 0)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

            return await this.entryRepo.GetByIdsAsync(ids);
        }
    }
}