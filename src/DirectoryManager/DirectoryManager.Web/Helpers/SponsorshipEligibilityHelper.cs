using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Pure, testable sponsorship-eligibility rules. A listing may sponsor only when it is
    /// Verified AND has been in the directory for at least the minimum number of days —
    /// unless it is "grandfathered" (a current/past sponsor), which always passes.
    /// Extracted from SponsorshipController so the rules can be unit-tested directly.
    /// </summary>
    public static class SponsorshipEligibilityHelper
    {
        /// <summary>Whole days a listing has been in the directory (never negative; 0 for an unknown/min date).</summary>
        public static int ComputeAgeDays(DateTime createDateUtc, DateTime nowUtc)
        {
            if (createDateUtc == DateTime.MinValue)
            {
                return 0;
            }

            var days = (int)Math.Floor((nowUtc - createDateUtc).TotalDays);
            return days < 0 ? 0 : days;
        }

        /// <summary>
        /// Returns whether the listing may advertise, plus the human-readable reasons it can't.
        /// Reasons are ordered: status first, then age.
        /// </summary>
        public static (bool CanAdvertise, List<string> Reasons) CheckEligibility(
            DirectoryStatus status,
            DateTime createDateUtc,
            bool grandfathered,
            int minimumDaysListed,
            DateTime nowUtc)
        {
            // Grandfather clause: a current or past sponsor can always sponsor again or extend.
            if (grandfathered)
            {
                return (true, new List<string>());
            }

            var reasons = new List<string>();

            // Requirement 1: must be Verified (green checkmark).
            if (status != DirectoryStatus.Verified)
            {
                reasons.Add(
                    $"Status is {status}. " +
                    "Listing must be Verified (green checkmark) to sponsor.");
            }

            // Requirement 2: must have been listed long enough.
            if (createDateUtc == DateTime.MinValue)
            {
                reasons.Add("Listing age is unknown (missing create date).");
            }
            else
            {
                var days = ComputeAgeDays(createDateUtc, nowUtc);
                if (days < minimumDaysListed)
                {
                    var daysLeft = minimumDaysListed - days;
                    reasons.Add(
                        $"Listing is too new. A listing must be in the directory at least " +
                        $"{minimumDaysListed} days to sponsor — {daysLeft} more day{(daysLeft == 1 ? string.Empty : "s")} to go.");
                }
            }

            return (reasons.Count == 0, reasons);
        }
    }
}
