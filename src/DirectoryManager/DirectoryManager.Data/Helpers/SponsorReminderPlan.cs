namespace DirectoryManager.Data.Helpers
{
    public enum SponsorReminderKind
    {
        None = 0,

        /// <summary>The first notice, sent once the sponsorship ends within the first window (72 hours).</summary>
        First = 1,

        /// <summary>The final, urgent notice, sent once 24 hours or less are left.</summary>
        Final = 2,
    }

    /// <summary>
    /// Decides which expiry reminder (if any) a sponsorship is due right now. Each reminder goes
    /// out at most once per paid period: the "sent" facts live on the period's invoice, and a
    /// renewal attaches a new invoice.
    /// </summary>
    public static class SponsorReminderPlan
    {
        public const int FirstWindowHours = 72;

        public const int FinalWindowHours = 24;

        /// <summary>The final notice waits at least this long after the first one, so a sponsor never gets both together.</summary>
        public const int MinHoursBetween = 12;

        public static SponsorReminderKind Due(
            DateTime nowUtc,
            DateTime campaignEndUtc,
            bool firstSent,
            DateTime? firstSentUtc,
            DateTime? finalSentUtc,
            int firstWindowHours = FirstWindowHours,
            int finalWindowHours = FinalWindowHours)
        {
            var left = campaignEndUtc - nowUtc;
            if (left <= TimeSpan.Zero)
            {
                return SponsorReminderKind.None;
            }

            if (left <= TimeSpan.FromHours(finalWindowHours))
            {
                if (finalSentUtc != null)
                {
                    return SponsorReminderKind.None;
                }

                // A short sponsorship can enter both windows at once; then only the final notice
                // goes out. If the first one went out a moment ago, wait for a later run.
                if (firstSentUtc is { } first && nowUtc - first < TimeSpan.FromHours(MinHoursBetween))
                {
                    return SponsorReminderKind.None;
                }

                return SponsorReminderKind.Final;
            }

            if (left <= TimeSpan.FromHours(firstWindowHours) && !firstSent && firstSentUtc == null && finalSentUtc == null)
            {
                return SponsorReminderKind.First;
            }

            return SponsorReminderKind.None;
        }

        /// <summary>Whole hours left, rounded to nearest, never below 1 (for "ends in about N hours").</summary>
        public static int HoursLeft(DateTime nowUtc, DateTime campaignEndUtc) =>
            Math.Max(1, (int)Math.Round((campaignEndUtc - nowUtc).TotalHours, MidpointRounding.AwayFromZero));
    }
}
