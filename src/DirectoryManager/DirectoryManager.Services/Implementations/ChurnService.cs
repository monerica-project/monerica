// ChurnService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Computes start-cohort churn for the inclusive window [StartUtc, EndUtc], with optional filters.
        /// Start cohort = advertisers active on StartUtc (had any paid day covering StartUtc).
        /// ChurnedFromStartCohort = start-cohort advertisers that are NOT active on EndUtc.
        /// </summary>
        public async Task<ChurnWindowResult> GetChurnAsync(
            ChurnWindowRequest request,
            CancellationToken ct = default)
        {
            var s = request.StartUtc.Date;
            var e = request.EndUtc.Date; // inclusive end day

            // Load PAID invoices directly from the context with optional filters.
            IQueryable<SponsoredListingInvoice> q = this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid);

            if (request.SponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == request.SponsorshipType.Value);
            }

            if (request.CategoryId.HasValue)
            {
                q = q.Where(i => i.CategoryId == request.CategoryId.Value);
            }

            if (request.SubCategoryId.HasValue)
            {
                q = q.Where(i => i.SubCategoryId == request.SubCategoryId.Value);
            }

            var paid = await q
                .Select(i => new
                {
                    i.DirectoryEntryId,
                    Start = i.CampaignStartDate.Date,
                    End = i.CampaignEndDate.Date,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // helper delegates over inclusive day ranges
            static bool CoversDay(DateTime start, DateTime end, DateTime dayUtc) =>
                start <= dayUtc && end >= dayUtc;

            static bool OverlapsWindow(DateTime start, DateTime end, DateTime startDayUtc, DateTime endDayUtc) =>
                end >= startDayUtc && start <= endDayUtc;

            var activeAtStart = paid
                .Where(i => CoversDay(i.Start, i.End, s))
                .Select(i => i.DirectoryEntryId)
                .Distinct()
                .ToHashSet();

            var activeAtEnd = paid
                .Where(i => CoversDay(i.Start, i.End, e))
                .Select(i => i.DirectoryEntryId)
                .Distinct()
                .ToHashSet();

            var activeInWindow = paid
                .Where(i => OverlapsWindow(i.Start, i.End, s, e))
                .Select(i => i.DirectoryEntryId)
                .Distinct()
                .ToHashSet();

            var activatedInWindow = new HashSet<int>(activeInWindow);
            activatedInWindow.ExceptWith(activeAtStart);

            var startCohortLost = new HashSet<int>(activeAtStart);
            startCohortLost.ExceptWith(activeAtEnd); // those not present at End

            // "Gross churn" = logos whose LAST paid day falls in the window
            var grossChurnInWindow = paid
                .GroupBy(i => i.DirectoryEntryId)
                .Select(g => new { g.Key, LastEnd = g.Max(x => x.End) })
                .Where(x => x.LastEnd >= s && x.LastEnd <= e)
                .Count();

            return new ChurnWindowResult
            {
                StartUtc = s,
                EndUtc = e,
                ActiveAtStart = activeAtStart.Count,
                Activated = activatedInWindow.Count,
                ActiveAtEnd = activeAtEnd.Count,
                UniqueActiveInWindow = activeInWindow.Count,
                ChurnedFromStartCohort = startCohortLost.Count,
                GrossChurnInWindow = grossChurnInWindow
            };
        }

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

            var startDay = windowStartUtc.Date;
            var endOpenDay = windowEndOpenUtc.Date;
            var endDayInclusive = endOpenDay.AddDays(-1);

            // Load and merge paid spans per advertiser (unchanged helpers).
            var spansByAdvertiser = await this.LoadPaidSpansAsync(
                sponsorshipType, subCategoryId, categoryId, ct).ConfigureAwait(false);

            var mergedByAdvertiser = spansByAdvertiser.ToDictionary(
                kvp => kvp.Key,
                kvp => MergeSpans(kvp.Value));

            // Build sets
            var startCohort = new HashSet<int>();
            var endCohort = new HashSet<int>();
            var activatedIds = new HashSet<int>();
            var grossChurnIds = new HashSet<int>();
            var uniqueActive = 0;

            foreach (var (advertiserId, intervals) in mergedByAdvertiser)
            {
                if (intervals.Count == 0)
                {
                    continue;
                }

                var firstStart = intervals[0].start;
                var lastEnd = intervals[^1].end;

                bool isActiveAtStart = intervals.Any(iv => ContainsDay(iv.start, iv.end, startDay));
                bool isActiveAtEnd = intervals.Any(iv => ContainsDay(iv.start, iv.end, endDayInclusive));
                bool anyOverlap = intervals.Any(iv => Overlaps(iv.start, iv.end, startDay, endDayInclusive));

                if (isActiveAtStart) startCohort.Add(advertiserId);
                if (isActiveAtEnd) endCohort.Add(advertiserId);
                if (anyOverlap) uniqueActive++;

                // Activated inside the window = first ever start within [start, endOpen)
                if (!isActiveAtStart && firstStart >= startDay && firstStart < endOpenDay)
                {
                    activatedIds.Add(advertiserId);
                }

                // Gross churn in window = last ever end within [start, endOpen)
                if (lastEnd >= startDay && lastEnd < endOpenDay)
                {
                    grossChurnIds.Add(advertiserId);
                }
            }

            // Start-cohort churners = in start cohort but not in end cohort
            var churnedStartCohort = new HashSet<int>(startCohort);
            churnedStartCohort.ExceptWith(endCohort);

            var activeAtStart = startCohort.Count;
            var activeAtEnd = endCohort.Count;

            var churnRate = activeAtStart > 0
                ? Math.Round((decimal)churnedStartCohort.Count / activeAtStart, 4)
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
                // IMPORTANT: this is now start-cohort churn count (not gross)
                ChurnedInWindow = churnedStartCohort.Count,
                ActiveAtEnd = activeAtEnd,
                UniqueActiveInWindow = uniqueActive,
                ChurnRate = churnRate,

                // Keep gross churn separately so you can show it as a count
                GrossChurnInWindow = grossChurnIds.Count,
                ActivatedDirectoryEntryIds = activatedIds.ToList(),
                ChurnedDirectoryEntryIds = churnedStartCohort.ToList(),
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

            var cur = new DateTime(startMonthUtc.Year, startMonthUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(endMonthOpenUtc.Year, endMonthOpenUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

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

            var dict = new Dictionary<int, List<(DateTime start, DateTime end)>>();

            foreach (var r in rows)
            {
                var s = r.Start.Date;
                var e = r.End.Date;
                if (e < s)
                {
                    e = s;
                }

                if (!dict.TryGetValue(r.DirectoryEntryId, out var list))
                {
                    list = new List<(DateTime start, DateTime end)>();
                    dict[r.DirectoryEntryId] = list;
                }

                list.Add((s, e));
            }

            return dict;
        }

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

                // Treat adjacent as continuous (end=10th, next start=11th).
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

        private static bool ContainsDay(DateTime s, DateTime e, DateTime day) =>
            s <= day && day <= e;

        private static bool Overlaps(DateTime s1, DateTime e1, DateTime s2, DateTime e2) =>
            s1 <= e2 && s2 <= e1;
    }
}
