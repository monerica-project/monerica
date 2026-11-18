using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// The one rule for the "New" label on listings: a publicly listed entry is "new" for
    /// <see cref="Days"/> days after it was added. The label ends at 00:00 UTC on the day the
    /// listing turns <see cref="Days"/> days old (whole UTC days, not the exact minute), so all
    /// labels that end on a given day end together. The sitemap uses <see cref="LatestEndAtOrBefore"/>
    /// to move its newest &lt;lastmod&gt; to that midnight, which makes the static mirror re-copy
    /// its pages once that day and drop the expired labels.
    /// </summary>
    public static class NewListingRule
    {
        /// <summary>How long a listing counts as new.</summary>
        public const int Days = 90;

        /// <summary>
        /// Statuses that can show the label: listings people can use. Removed (not shown at all),
        /// Unknown and Scam (crossed out) never do.
        /// </summary>
        public static bool IsEligibleStatus(DirectoryStatus status) =>
            status is DirectoryStatus.Admitted or DirectoryStatus.Verified or DirectoryStatus.Questionable;

        /// <summary>The moment the label disappears: 00:00 UTC, <see cref="Days"/> days after the add date.</summary>
        public static DateTime EndsUtc(DateTime createDateUtc) =>
            DateTime.SpecifyKind(createDateUtc.Date, DateTimeKind.Utc).AddDays(Days);

        /// <summary>True when the listing should show the "New" label at <paramref name="nowUtc"/>.</summary>
        public static bool IsNew(DateTime createDateUtc, DirectoryStatus status, DateTime nowUtc)
        {
            if (!IsEligibleStatus(status) || createDateUtc <= DateTime.MinValue.AddDays(1))
            {
                return false;
            }

            return nowUtc < EndsUtc(createDateUtc);
        }

        /// <summary>
        /// The most recent moment (at or before <paramref name="nowUtc"/>) when any eligible
        /// listing's label ended, or null when none has. Pages changed at that moment.
        /// </summary>
        public static DateTime? LatestEndAtOrBefore(
            IEnumerable<(DateTime CreateDate, DirectoryStatus Status)> listings,
            DateTime nowUtc)
        {
            DateTime? latest = null;
            foreach (var (createDate, status) in listings ?? Enumerable.Empty<(DateTime, DirectoryStatus)>())
            {
                if (!IsEligibleStatus(status) || createDate <= DateTime.MinValue.AddDays(1))
                {
                    continue;
                }

                var ends = EndsUtc(createDate);
                if (ends <= nowUtc && (latest == null || ends > latest.Value))
                {
                    latest = ends;
                }
            }

            return latest;
        }
    }
}
