using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>Who is looking at sponsoring their listing, and when (admin only).</summary>
    [Authorize]
    [Route("admin/sponsor-interest")]
    public class SponsorInterestController : Controller
    {
        private const int DefaultDays = 90;
        private const int RecentTake = 150;

        private readonly ISponsorInterestLogRepository logRepo;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly ICategoryRepository categoryRepo;
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly IVideoSponsorshipRepository videoRepo;

        public SponsorInterestController(
            ISponsorInterestLogRepository logRepo,
            IDirectoryEntryRepository entryRepo,
            ICategoryRepository categoryRepo,
            ISubcategoryRepository subcategoryRepo,
            IVideoSponsorshipRepository videoRepo)
        {
            this.logRepo = logRepo;
            this.entryRepo = entryRepo;
            this.categoryRepo = categoryRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.videoRepo = videoRepo;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index([FromQuery] DateTime? start, [FromQuery] DateTime? end, [FromQuery] string? kind)
        {
            bool video = string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase);
            var endDate = (end ?? DateTime.UtcNow).Date;
            var startDate = (start ?? endDate.AddDays(-DefaultDays)).Date;
            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var fromUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(endDate.AddDays(1), DateTimeKind.Utc);

            // Site sponsors and video sponsorship are separate funnels: show one at a time.
            var logs = (await this.logRepo.GetInRangeAsync(fromUtc, toUtc)).Where(l => l.Step.IsVideo() == video).ToList();
            var paid = video
                ? (await this.videoRepo.ListPaidOrdersAsync())
                    .Where(o => o.PaidUtc is DateTime p && p >= fromUtc && p < toUtc)
                    .GroupBy(o => o.DirectoryEntryId)
                    .ToDictionary(g => g.Key, g => g.Max(o => o.PaidUtc!.Value))
                : await this.logRepo.GetPaidEntryIdsAsync(fromUtc, toUtc);
            var offerNames = video
                ? (await this.videoRepo.GetOffersAsync(enabledOnly: false)).ToDictionary(o => o.VideoSponsorOfferId, o => o.Name)
                : new Dictionary<int, string>();
            var entries = await this.entryRepo.GetByIdsAsync(logs.Select(l => l.DirectoryEntryId).Distinct());

            var catNames = (await this.categoryRepo.GetAllAsync()).ToDictionary(c => c.CategoryId, c => c.Name);
            var subLabels = (await this.subcategoryRepo.GetAllAsync()).ToDictionary(
                s => s.SubCategoryId,
                s => $"{(catNames.TryGetValue(s.CategoryId, out var cn) ? cn : "#" + s.CategoryId)} › {s.Name}");

            string Placement(SponsorInterestLog l) => l.Step.IsVideo()
                ? (l.TypeId is int o && offerNames.TryGetValue(o, out var on) ? on : "Video types page")
                : l.SponsorshipType switch
            {
                SponsorshipType.MainSponsor => "Main",
                SponsorshipType.CategorySponsor => "Category: " + (l.TypeId is int c && catNames.TryGetValue(c, out var n) ? n : "?"),
                SponsorshipType.SubcategorySponsor => "Subcategory: " + (l.TypeId is int s && subLabels.TryGetValue(s, out var sn) ? sn : "?"),
                _ => "Options page",
            };

            string Name(int id) => entries.TryGetValue(id, out var e) ? e.Name : "#" + id;

            var vm = new SponsorInterestReportViewModel
            {
                IsVideo = video,
                StartDate = startDate,
                EndDate = endDate,
                TotalLooks = logs.Count,
                ListingCount = logs.Select(l => l.DirectoryEntryId).Distinct().Count(),
                StartedPaymentListings = logs.Where(l => l.Step.IsStartedPayment()).Select(l => l.DirectoryEntryId).Distinct().Count(),
                PaidListings = paid.Count,
            };

            vm.Weeks = logs
                .GroupBy(l => WeekStart(l.CreateDate))
                .Select(g => new SponsorInterestWeek
                {
                    WeekStart = g.Key,
                    Looks = g.Count(),
                    Listings = g.Select(x => x.DirectoryEntryId).Distinct().Count(),
                    StartedPayment = g.Where(x => x.Step.IsStartedPayment()).Select(x => x.DirectoryEntryId).Distinct().Count(),
                })
                .OrderByDescending(w => w.WeekStart)
                .ToList();

            vm.Listings = logs
                .GroupBy(l => l.DirectoryEntryId)
                .Select(g =>
                {
                    var furthest = g.Max(x => x.Step);
                    entries.TryGetValue(g.Key, out var e);
                    return new SponsorInterestListingRow
                    {
                        DirectoryEntryId = g.Key,
                        Name = Name(g.Key),
                        ProfilePath = e != null ? "/site/" + e.DirectoryEntryKey : null,
                        Placements = g.Where(x => x.Step.IsVideo() ? x.TypeId is > 0 : x.SponsorshipType != SponsorshipType.Unknown)
                            .Select(Placement).Distinct().OrderBy(x => x).ToList(),
                        FurthestStep = StepLabel(furthest),
                        FurthestStepValue = (int)furthest,
                        StartedPayment = furthest.IsStartedPayment(),
                        Looks = g.Count(),
                        Days = g.Select(x => x.CreateDate.Date).Distinct().Count(),
                        FirstSeen = g.Min(x => x.CreateDate),
                        LastSeen = g.Max(x => x.CreateDate),
                        PaidDate = paid.TryGetValue(g.Key, out var pd) ? pd : null,
                    };
                })
                .OrderByDescending(r => r.LastSeen)
                .ToList();

            vm.Recent = logs
                .OrderByDescending(l => l.CreateDate)
                .Take(RecentTake)
                .Select(l => new SponsorInterestEvent
                {
                    When = l.CreateDate,
                    DirectoryEntryId = l.DirectoryEntryId,
                    Name = Name(l.DirectoryEntryId),
                    Placement = Placement(l),
                    Step = StepLabel(l.Step),
                })
                .ToList();

            return this.View(vm);
        }

        internal static DateTime WeekStart(DateTime d)
        {
            int diff = ((int)d.DayOfWeek + 6) % 7; // Monday-start weeks
            return d.Date.AddDays(-diff);
        }

        internal static string StepLabel(SponsorInterestStep step) => step switch
        {
            SponsorInterestStep.ViewedOptions => "Viewed options",
            SponsorInterestStep.ViewedPrices => "Viewed prices",
            SponsorInterestStep.PickedDuration => "Picked a duration",
            SponsorInterestStep.StartedPayment => "Started payment",
            SponsorInterestStep.VideoViewedPrices => "Viewed prices",
            SponsorInterestStep.VideoPickedVideos => "Picking videos",
            SponsorInterestStep.VideoReviewedOrder => "Reviewing order",
            SponsorInterestStep.VideoStartedPayment => "Started payment",
            _ => "Unknown",
        };
    }
}
