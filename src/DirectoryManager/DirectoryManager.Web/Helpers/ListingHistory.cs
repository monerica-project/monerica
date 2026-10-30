using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>How finely to sample a date range when charting it.</summary>
    public enum ReportStep
    {
        Day,
        Week,
        Month,
    }

    /// <summary>
    /// Rebuilds what the directory looked like on any past date from the listing edit history
    /// (DirectoryEntriesAudit). Every admin edit stores a full snapshot of the listing, so a
    /// listing's state on a date is its newest snapshot taken on or before that date.
    /// "Active" = Admitted, Verified, Questionable or Scam (anything but Removed/Unknown).
    /// </summary>
    public sealed class ListingHistory
    {
        private readonly Dictionary<int, List<(DateTime At, ListingState State)>> timelines;

        public ListingHistory(IEnumerable<DirectoryEntriesAudit> audits)
        {
            this.timelines = (audits ?? Enumerable.Empty<DirectoryEntriesAudit>())
                .Select(a => (
                    At: a.UpdateDate ?? a.CreateDate,
                    Seq: a.DirectoryEntriesAuditId,
                    State: new ListingState(
                        a.DirectoryEntryId,
                        a.DirectoryStatus,
                        NormalizeCountry(a.CountryCode),
                        a.SubCategoryId)))
                .GroupBy(x => x.State.EntryId)
                .ToDictionary(
                    g => g.Key,

                    // Snapshots saved in the same instant keep their save order (audit id).
                    g => g.OrderBy(x => x.At).ThenBy(x => x.Seq).Select(x => (x.At, x.State)).ToList());

            this.Earliest = this.timelines.Count == 0
                ? DateTime.UtcNow.Date
                : this.timelines.Values.Min(t => t[0].At).Date;
        }

        /// <summary>Date of the oldest snapshot (start of the available history).</summary>
        public DateTime Earliest { get; }

        public static bool IsActive(DirectoryStatus s) =>
            s is DirectoryStatus.Admitted or DirectoryStatus.Verified
              or DirectoryStatus.Questionable or DirectoryStatus.Scam;

        /// <summary>Daily for up to ~3 months, weekly up to 2 years, monthly beyond.</summary>
        public static ReportStep StepFor(DateTime start, DateTime end)
        {
            var days = (end.Date - start.Date).TotalDays;
            return days <= 92 ? ReportStep.Day : days <= 731 ? ReportStep.Week : ReportStep.Month;
        }

        /// <summary>The state of every listing that was active at the end of <paramref name="date"/>.</summary>
        public IReadOnlyList<ListingState> ActiveOn(DateTime date)
        {
            var cutoff = EndOfDay(date);
            var result = new List<ListingState>();
            foreach (var timeline in this.timelines.Values)
            {
                var state = StateAt(timeline, cutoff);
                if (state is not null && IsActive(state.Status))
                {
                    result.Add(state);
                }
            }

            return result;
        }

        /// <summary>Active-listing counts sampled across the range (always includes both ends).</summary>
        public IReadOnlyList<(DateTime Date, int Active)> ActiveSeries(DateTime start, DateTime end, ReportStep step)
        {
            var points = new List<DateTime>();
            for (var d = start.Date; d < end.Date; d = Next(d, step))
            {
                points.Add(d);
            }

            points.Add(end.Date);
            return points.Select(d => (d, this.ActiveOn(d).Count)).ToList();
        }

        /// <summary>
        /// Listings that became active during the range (not active at the start, active at the end)
        /// and listings that stopped being active (active at the start, not at the end).
        /// </summary>
        public (IReadOnlyList<int> Added, IReadOnlyList<int> Removed) Changes(DateTime start, DateTime end)
        {
            var before = this.ActiveOn(start.Date.AddDays(-1)).Select(s => s.EntryId).ToHashSet();
            var after = this.ActiveOn(end).Select(s => s.EntryId).ToHashSet();
            return (after.Except(before).ToList(), before.Except(after).ToList());
        }

        public static DateTime EndOfDay(DateTime date) => date.Date.AddDays(1).AddTicks(-1);

        private static DateTime Next(DateTime d, ReportStep step) => step switch
        {
            ReportStep.Day => d.AddDays(1),
            ReportStep.Week => d.AddDays(7),
            _ => d.AddMonths(1),
        };

        private static ListingState? StateAt(List<(DateTime At, ListingState State)> timeline, DateTime cutoff)
        {
            // Snapshots are sorted by time; binary search for the last one at or before the cutoff.
            int lo = 0, hi = timeline.Count - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (timeline[mid].At <= cutoff)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found < 0 ? null : timeline[found].State;
        }

        private static string? NormalizeCountry(string? code)
        {
            var c = (code ?? string.Empty).Trim().ToUpperInvariant();
            return c.Length == 0 ? null : c;
        }
    }

    /// <summary>A listing's state at a point in time.</summary>
    public sealed record ListingState(int EntryId, DirectoryStatus Status, string? CountryCode, int? SubCategoryId);
}
