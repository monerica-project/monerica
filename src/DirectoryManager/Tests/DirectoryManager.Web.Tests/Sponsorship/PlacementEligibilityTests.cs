using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    // Waitlist "Eligible now": the same rules checkout uses, for one exact placement.
    public class PlacementEligibilityTests
    {
        private static readonly DateTime Now = new (2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Old = Now.AddDays(-400);
        private const int Min = 90;
        private const int Hosting = 22;
        private const int Vps = 166;
        private const int Tor = 171;

        private static (bool Eligible, string? Reason) Check(
            SponsorshipType type, int? typeId, DirectoryStatus status = DirectoryStatus.Verified, DateTime? created = null,
            bool mainOff = false, bool active = false, params (int, bool)[] alts) =>
            SponsorshipEligibilityHelper.ForPlacement(type, typeId, status, created ?? Old, Vps, Hosting, mainOff, alts, active, Min, Now);

        [Fact]
        public void VerifiedOldListing_InItsCategory_IsEligible() =>
            Assert.True(Check(SponsorshipType.CategorySponsor, Hosting).Eligible);

        [Fact]
        public void OtherCategory_IsNot()
        {
            var r = Check(SponsorshipType.CategorySponsor, 8);
            Assert.False(r.Eligible);
            Assert.Equal("Not in this category", r.Reason);
        }

        [Fact]
        public void NotVerified_IsNot_UnlessActiveSponsor()
        {
            Assert.Equal("Admitted", Check(SponsorshipType.MainSponsor, null, DirectoryStatus.Admitted).Reason);
            Assert.Equal("Scam · 60 days to go", Check(SponsorshipType.MainSponsor, null, DirectoryStatus.Scam, Now.AddDays(-30)).Reason);
            Assert.True(Check(SponsorshipType.MainSponsor, null, DirectoryStatus.Admitted, active: true).Eligible);
        }

        [Fact]
        public void TooNew_SaysHowLong()
        {
            var r = Check(SponsorshipType.MainSponsor, null, created: Now.AddDays(-30));
            Assert.False(r.Eligible);
            Assert.Equal("60 days to go", r.Reason);
        }

        [Fact]
        public void Subcategory_MainOrAdditional()
        {
            Assert.True(Check(SponsorshipType.SubcategorySponsor, Vps).Eligible);
            Assert.True(Check(SponsorshipType.SubcategorySponsor, Tor, alts: (Tor, false)).Eligible);
            Assert.False(Check(SponsorshipType.SubcategorySponsor, Tor, alts: (Tor, true)).Eligible);
            Assert.Equal("Not in this subcategory", Check(SponsorshipType.SubcategorySponsor, 999).Reason);
        }

        [Fact]
        public void SponsorshipOffForItsSubcategory_BlocksMainAndCategory()
        {
            Assert.False(Check(SponsorshipType.MainSponsor, null, mainOff: true).Eligible);
            Assert.False(Check(SponsorshipType.CategorySponsor, Hosting, mainOff: true).Eligible);
        }
    }
}
