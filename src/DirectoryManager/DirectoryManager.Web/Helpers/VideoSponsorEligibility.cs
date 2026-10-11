using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Who may buy a video sponsorship: the same Verified + minimum-days rule as site sponsors,
    /// applied strictly (no past-sponsor exception), and never for a listing whose subcategory
    /// has sponsorship purchasing disabled.
    /// </summary>
    public static class VideoSponsorEligibility
    {
        public static (bool CanSponsor, List<string> Reasons) Check(DirectoryEntry entry, DateTime nowUtc)
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                entry.DirectoryStatus,
                entry.CreateDate,
                grandfathered: false,
                DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring,
                nowUtc);

            if (entry.SubCategory?.SponsorshipDisabled == true)
            {
                reasons.Add("Sponsorship is not available for listings in this subcategory.");
                ok = false;
            }

            return (ok, reasons);
        }
    }
}
