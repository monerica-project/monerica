// ChurnService.cs
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Services.Models.TransferModels;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Computes advertiser churn metrics over arbitrary windows and monthly periods.
    /// Uses paid invoice campaign windows, merged into non-overlapping intervals per advertiser.
    /// </summary>
    public sealed class ChurnService : IChurnService
    {
        private readonly IApplicationDbContext context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChurnService"/> class.
        /// </summary>
        /// <param name="context">Application DB context.</param>
        public ChurnService(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc/>
        public async Task<ChurnMetrics> GetChurnForWindowAsync(
            DateTime windowStartUtc,
            DateTime windowEndOpenUtc,
            SponsorshipType? sponsorshipType = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default)
        {
            if (windowEndOpenUtc <= windowStartUtc)
            {
                throw new ArgumentException("Window end must be greater than window start.", nameof(windowEndOpenUtc));
            }

            // Normalize to date granularity (campaigns are considered inclusive of their end day).
            var startDay = windowStartUtc.Date;
            var endOpenDay = windowEndOpenUtc.Date;
            var endDayInclusive = endOpenDay.AddDays(-1);

            // Pull minimal paid rows for the (optionally) filtered scope.
            var spansByAdvertiser = await this.LoadPaidSpansAsync(
                sponsorshipType,
                subCategoryId,
                categoryId,
                ct).ConfigureAwait(false);

            // Merge each advertiser's spans into non-overlapping intervals.
            var mergedByAdvertiser = spansByAdvertiser.ToDictionary(
                kvp => kvp.Key,
                kvp => MergeSpans(kvp.Value));

            // Metrics.
            var activeAtStart = 0;
            var activatedIds = new List<int>();
            var churnedIds = new List<int>();
            var activeAtEnd = 0;
            var uniqueActive = 0;

            foreach (var kvp in mergedByAdvertiser)
            {
                var advertiserId = kvp.Key;
                var intervals = kvp.Value;

                if (intervals.Count == 0)
                {
                    continue;
                }

                var firstStart = intervals[0].start;
                var lastEnd = intervals[^1].end;

                var isActiveAtStart = intervals.Any(iv => ContainsDay(iv.start, iv.end, startDay));
                var isActiveAtEnd = intervals.Any(iv => ContainsDay(iv.start, iv.end, endDayInclusive));
                var anyOverlapInWindow = intervals.Any(iv => Overlaps(iv.start, iv.end, startDay, endDayInclusive));

                if (isActiveAtStart)
                {
                    activeAtStart++;
                }

                if (isActiveAtEnd)
                {
                    activeAtEnd++;
                }

                if (anyOverlapInWindow)
                {
                    uniqueActive++;
                }

                // Activated in window: first-ever active day falls inside the window and they were not active at start.
                if (!isActiveAtStart && firstStart >= startDay && firstStart < endOpenDay)
                {
                    activatedIds.Add(advertiserId);
                }

                // Churned in window: last-ever active day falls inside the window.
                if (lastEnd >= startDay && lastEnd < endOpenDay)
                {
                    churnedIds.Add(advertiserId);
                }
            }

            var churnRate = activeAtStart > 0
                ? Math.Round((decimal)churnedIds.Count / activeAtStart, 4)
                : 0m;

            return new ChurnMetrics
            {
                WindowStartUtc = startDay,
                WindowEndOpenUtc = endOpenDay,
                SponsorshipType = sponsorshipType,
                SubCategoryId = subCategoryId,
                CategoryId = categoryId,
                ActiveAtStart = activeAtStart,
                ActivatedInWindow = activatedIds.Count,
                ChurnedInWindow = churnedIds.Count,
                ActiveAtEnd = activeAtEnd,
                UniqueActiveInWindow = uniqueActive,
                ChurnRate = churnRate,
                ActivatedDirectoryEntryIds = activatedIds,
                ChurnedDirectoryEntryIds = churnedIds,
            };
        }

        /// <inheritdoc/>
        public Task<ChurnMetrics> GetMonthlyChurnAsync(
            DateTime monthStartUtc,
            SponsorshipType? sponsorshipType = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default)
        {
            var start = new DateTime(monthStartUtc.Year, monthStartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOpen = start.AddMonths(1);
            return this.GetChurnForWindowAsync(start, endOpen, sponsorshipType, subCategoryId, categoryId, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ChurnMetricsPoint>> GetMonthlyChurnSeriesAsync(
            DateTime startMonthUtc,
            DateTime endMonthOpenUtc,
            SponsorshipType? sponsorshipType = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default)
        {
            if (endMonthOpenUtc <= startMonthUtc)
            {
                throw new ArgumentException("Series end must be greater than series start.", nameof(endMonthOpenUtc));
            }

            // Normalize to calendar month boundaries (UTC).
            var cur = new DateTime(startMonthUtc.Year, startMonthUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(endMonthOpenUtc.Year, endMonthOpenUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Preload all paid spans once for performance; reuse for every month in the series.
            var spansByAdvertiser = await this.LoadPaidSpansAsync(
                sponsorshipType,
                subCategoryId,
                categoryId,
                ct).ConfigureAwait(false);

            var mergedByAdvertiser = spansByAdvertiser.ToDictionary(
                kvp => kvp.Key,
                kvp => MergeSpans(kvp.Value));

            var points = new List<ChurnMetricsPoint>(capacity: 24);

            while (cur < end)
            {
                var monthStart = cur.Date;
                var monthEndOpen = cur.AddMonths(1).Date;
                var monthEndInclusive = monthEndOpen.AddDays(-1);

                var activeAtStart = 0;
                var activated = 0;
                var churned = 0;
                var activeAtEnd = 0;

                foreach (var intervals in mergedByAdvertiser.Values)
                {
                    if (intervals.Count == 0)
                    {
                        continue;
                    }

                    var firstStart = intervals[0].start;
                    var lastEnd = intervals[^1].end;

                    var isActiveAtStart = intervals.Any(iv => ContainsDay(iv.start, iv.end, monthStart));
                    var isActiveAtEnd = intervals.Any(iv => ContainsDay(iv.start, iv.end, monthEndInclusive));

                    if (isActiveAtStart)
                    {
                        activeAtStart++;
                    }

                    if (isActiveAtEnd)
                    {
                        activeAtEnd++;
                    }

                    if (!isActiveAtStart && firstStart >= monthStart && firstStart < monthEndOpen)
                    {
                        activated++;
                    }

                    if (lastEnd >= monthStart && lastEnd < monthEndOpen)
                    {
                        churned++;
                    }
                }

                var churnRate = activeAtStart > 0
                    ? Math.Round((decimal)churned / activeAtStart, 4)
                    : 0m;

                points.Add(new ChurnMetricsPoint
                {
                    PeriodStartUtc = monthStart,
                    PeriodEndOpenUtc = monthEndOpen,
                    SponsorshipType = sponsorshipType,
                    ActiveAtStart = activeAtStart,
                    ActivatedInPeriod = activated,
                    ChurnedInPeriod = churned,
                    ActiveAtEnd = activeAtEnd,
                    ChurnRate = churnRate,
                });

                cur = monthEndOpen;
            }

            return points;
        }

        /// <summary>
        /// Loads paid campaign spans, filtered by optional scope, keyed by advertiser (DirectoryEntryId).
        /// </summary>
        private async Task<Dictionary<int, List<(DateTime start, DateTime end)>>> LoadPaidSpansAsync(
            SponsorshipType? sponsorshipType,
            int? subCategoryId,
            int? categoryId,
            CancellationToken ct)
        {
            IQueryable<SponsoredListingInvoice> q = this.context.SponsoredListingInvoices.AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            if (subCategoryId.HasValue)
            {
                q = q.Where(i => i.SubCategoryId == subCategoryId.Value);
            }

            if (categoryId.HasValue)
            {
                q = q.Where(i => i.CategoryId == categoryId.Value);
            }

            var rows = await q
                .Select(i => new
                {
                    i.DirectoryEntryId,
                    Start = i.CampaignStartDate,
                    End = i.CampaignEndDate,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var dict = new Dictionary<int, List<(DateTime start, DateTime end)>>(capacity: rows.Count);

            foreach (var r in rows)
            {
                // Normalize to day granularity, inclusive ranges.
                var s = r.Start.Date;
                var e = r.End.Date;
                if (e < s)
                {
                    e = s;
                }

                if (!dict.TryGetValue(r.DirectoryEntryId, out var list))
                {
                    list = new List<(DateTime start, DateTime end)>(capacity: 2);
                    dict[r.DirectoryEntryId] = list;
                }

                list.Add((s, e));
            }

            return dict;
        }

        /// <summary>
        /// Merges overlapping/adjacent day ranges into non-overlapping intervals.
        /// </summary>
        private static List<(DateTime start, DateTime end)> MergeSpans(List<(DateTime start, DateTime end)> spans)
        {
            if (spans.Count <= 1)
            {
                return spans.OrderBy(s => s.start).ToList();
            }

            var ordered = spans.OrderBy(s => s.start).ThenBy(s => s.end).ToList();
            var merged = new List<(DateTime start, DateTime end)>(ordered.Count);

            var curStart = ordered[0].start;
            var curEnd = ordered[0].end;

            for (var i = 1; i < ordered.Count; i++)
            {
                var (s, e) = ordered[i];

                // Adjacent days are considered continuous (e.g., end=10th, next start=11th).
                if (s <= curEnd.AddDays(1))
                {
                    if (e > curEnd)
                    {
                        curEnd = e;
                    }
                }
                else
                {
                    merged.Add((curStart, curEnd));
                    curStart = s;
                    curEnd = e;
                }
            }

            merged.Add((curStart, curEnd));
            return merged;
        }

        /// <summary>
        /// Returns true if the inclusive range [s, e] contains the given day.
        /// </summary>
        private static bool ContainsDay(DateTime s, DateTime e, DateTime day) =>
            s <= day && day <= e;

        /// <summary>
        /// Returns true if the two inclusive day ranges overlap.
        /// </summary>
        private static bool Overlaps(DateTime s1, DateTime e1, DateTime s2, DateTime e2) =>
            s1 <= e2 && s2 <= e1;
    }
}
