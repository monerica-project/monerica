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
        /// The one rule for "can this listing buy a sponsorship right now": Verified and listed at
        /// least <paramref name="minimumDaysListed"/> days, or a current sponsor (who may always
        /// extend). Used by checkout and by the "Advertise here" buttons, so they never disagree.
        /// </summary>
        public static bool CanBuy(DirectoryStatus status, DateTime createDateUtc, bool isActiveSponsor, int minimumDaysListed, DateTime nowUtc)
        {
            if (isActiveSponsor)
            {
                return true;
            }

            return status == DirectoryStatus.Verified
                && createDateUtc != DateTime.MinValue
                && (nowUtc - createDateUtc).TotalDays >= minimumDaysListed;
        }

        /// <summary>
        /// Can this listing buy this exact placement right now? Same rules as checkout and the
        /// "Advertise here" buttons: <see cref="CanBuy"/>, sponsorship not switched off for the
        /// subcategory it buys through, and the listing actually sits in that placement (main
        /// sponsor: any listing; category: its main subcategory's category; subcategory: its main
        /// or one of its additional subcategories). Returns a short reason when it can't.
        /// </summary>
        public static (bool Eligible, string? Reason) ForPlacement(
            SponsorshipType type,
            int? typeId,
            DirectoryStatus status,
            DateTime createDateUtc,
            int mainSubCategoryId,
            int? mainCategoryId,
            bool mainSponsorshipDisabled,
            IReadOnlyCollection<(int SubCategoryId, bool SponsorshipDisabled)> additionalSubcategories,
            bool isActiveSponsor,
            int minimumDaysListed,
            DateTime nowUtc)
        {
            if (!CanBuy(status, createDateUtc, isActiveSponsor, minimumDaysListed, nowUtc))
            {
                // Short: the status when it isn't Verified, and how many days until it's old enough.
                var parts = new List<string>();
                if (status != DirectoryStatus.Verified)
                {
                    parts.Add(status.ToString());
                }

                var daysLeft = minimumDaysListed - ComputeAgeDays(createDateUtc, nowUtc);
                if (createDateUtc == DateTime.MinValue)
                {
                    parts.Add("age unknown");
                }
                else if (daysLeft > 0)
                {
                    parts.Add($"{daysLeft} day{(daysLeft == 1 ? string.Empty : "s")} to go");
                }

                return (false, string.Join(" · ", parts));
            }

            switch (type)
            {
                case SponsorshipType.MainSponsor:
                    return mainSponsorshipDisabled ? (false, "Sponsorship is off for its subcategory") : (true, null);

                case SponsorshipType.CategorySponsor:
                    if (mainSponsorshipDisabled)
                    {
                        return (false, "Sponsorship is off for its subcategory");
                    }

                    return typeId is int c && mainCategoryId == c ? (true, null) : (false, "Not in this category");

                case SponsorshipType.SubcategorySponsor:
                    if (typeId is not int sub)
                    {
                        return (false, "No subcategory given");
                    }

                    if (mainSubCategoryId == sub)
                    {
                        return mainSponsorshipDisabled ? (false, "Sponsorship is off for this subcategory") : (true, null);
                    }

                    foreach (var (altId, altDisabled) in additionalSubcategories)
                    {
                        if (altId == sub)
                        {
                            return altDisabled ? (false, "Sponsorship is off for this subcategory") : (true, null);
                        }
                    }

                    return (false, "Not in this subcategory");

                default:
                    return (false, "Unknown placement");
            }
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
