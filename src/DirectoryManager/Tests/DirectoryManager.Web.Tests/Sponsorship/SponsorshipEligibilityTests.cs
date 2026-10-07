using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    /// <summary>
    /// The sponsorship eligibility gate: a listing may sponsor only when it is Verified AND has
    /// been in the directory at least the minimum number of days — unless grandfathered.
    /// Happy + sad paths.
    /// </summary>
    public class SponsorshipEligibilityTests
    {
        private static readonly DateTime Now = new (2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
        private const int MinDays = 180;

        [Fact] // HAPPY: verified + old enough → eligible, no reasons.
        public void VerifiedAndOldEnough_IsEligible()
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Verified, Now.AddDays(-200), grandfathered: false, MinDays, Now);

            Assert.True(ok);
            Assert.Empty(reasons);
        }

        [Fact] // Boundary: exactly the minimum age → eligible.
        public void ExactlyMinimumDays_IsEligible()
        {
            var (ok, _) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Verified, Now.AddDays(-MinDays), grandfathered: false, MinDays, Now);

            Assert.True(ok);
        }

        [Fact] // SAD: verified but too new → one "too new" reason with the days-left count.
        public void TooNew_NotEligible_ReportsDaysLeft()
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Verified, Now.AddDays(-175), grandfathered: false, MinDays, Now);

            Assert.False(ok);
            Assert.Single(reasons);
            Assert.Contains("too new", reasons[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("5 more days", reasons[0]); // 180 - 175
            Assert.DoesNotContain("days listed", reasons[0]); // the confusing wording we removed
        }

        [Fact] // SAD: one day short uses singular "day".
        public void OneDayShort_UsesSingularDay()
        {
            var (_, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Verified, Now.AddDays(-179), grandfathered: false, MinDays, Now);

            Assert.Contains("1 more day ", reasons[0]); // "1 more day to go" (singular)
        }

        [Theory] // SAD: not verified → status reason.
        [InlineData(DirectoryStatus.Admitted)]
        [InlineData(DirectoryStatus.Unknown)]
        [InlineData(DirectoryStatus.Questionable)]
        public void NotVerified_NotEligible(DirectoryStatus status)
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                status, Now.AddDays(-300), grandfathered: false, MinDays, Now);

            Assert.False(ok);
            Assert.Contains(reasons, r => r.Contains("Verified"));
        }

        [Fact] // SAD: not verified AND too new → two reasons, status first then age.
        public void NotVerifiedAndTooNew_TwoReasons_StatusFirst()
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Admitted, Now.AddDays(-10), grandfathered: false, MinDays, Now);

            Assert.False(ok);
            Assert.Equal(2, reasons.Count);
            Assert.Contains("Verified", reasons[0]);
            Assert.Contains("too new", reasons[1], StringComparison.OrdinalIgnoreCase);
        }

        [Fact] // HAPPY: grandfathered bypasses every requirement (even removed + brand new).
        public void Grandfathered_AlwaysEligible()
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Removed, Now, grandfathered: true, MinDays, Now);

            Assert.True(ok);
            Assert.Empty(reasons);
        }

        [Fact] // SAD: missing create date → "age is unknown".
        public void MissingCreateDate_AgeUnknown()
        {
            var (ok, reasons) = SponsorshipEligibilityHelper.CheckEligibility(
                DirectoryStatus.Verified, DateTime.MinValue, grandfathered: false, MinDays, Now);

            Assert.False(ok);
            Assert.Contains(reasons, r => r.Contains("age is unknown"));
        }

        [Fact]
        public void ComputeAgeDays_FloorsAndClampsNegative()
        {
            Assert.Equal(10, SponsorshipEligibilityHelper.ComputeAgeDays(Now.AddDays(-10.9), Now)); // floor down
            Assert.Equal(0, SponsorshipEligibilityHelper.ComputeAgeDays(Now.AddDays(5), Now));       // future date → 0
            Assert.Equal(0, SponsorshipEligibilityHelper.ComputeAgeDays(DateTime.MinValue, Now));    // unknown → 0
        }
    }
}
