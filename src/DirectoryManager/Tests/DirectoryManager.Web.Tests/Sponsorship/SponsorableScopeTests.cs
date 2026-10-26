using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Services.Implementations;

namespace DirectoryManager.Web.Tests.Sponsorship
{
    // "Advertise here" is only offered on pages where at least one listing could actually buy.
    public class SponsorableScopeTests
    {
        private const int MinDays = 180;
        private static readonly DateTime Now = new (2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

        private static readonly Dictionary<int, Subcategory> Subs = new ()
        {
            [10] = new Subcategory { SubCategoryId = 10, CategoryId = 1, Name = "Instant Swaps", SubCategoryKey = "instant-swaps" },
            [11] = new Subcategory { SubCategoryId = 11, CategoryId = 1, Name = "P2P", SubCategoryKey = "p2p" },
            [20] = new Subcategory { SubCategoryId = 20, CategoryId = 2, Name = "Resellers", SubCategoryKey = "resellers" },
            [30] = new Subcategory { SubCategoryId = 30, CategoryId = 3, Name = "Gambling", SubCategoryKey = "gambling", SponsorshipDisabled = true },
        };

        [Fact]
        public void VerifiedOldListing_MakesItsSubcategoryAndCategorySponsorable()
        {
            var s = Compute(new[] { Entry(1, 10, DirectoryStatus.Verified, 365) });

            Assert.Contains(10, s.Subcategories);
            Assert.Contains(1, s.Categories);
            Assert.DoesNotContain(11, s.Subcategories);
        }

        [Theory]
        [InlineData(DirectoryStatus.Admitted, 365)] // not Verified
        [InlineData(DirectoryStatus.Verified, 30)]  // too new
        public void NoQualifyingListing_NoAdvertiseHere(DirectoryStatus status, int ageDays)
        {
            var s = Compute(new[] { Entry(1, 20, status, ageDays) });

            Assert.Empty(s.Subcategories);
            Assert.Empty(s.Categories);
        }

        [Fact]
        public void CurrentSponsor_CountsEvenIfItNoLongerQualifies()
        {
            var s = Compute(new[] { Entry(1, 20, DirectoryStatus.Admitted, 10) }, active: new HashSet<int> { 1 });

            Assert.Contains(20, s.Subcategories);
            Assert.Contains(2, s.Categories);
        }

        [Fact]
        public void SponsorshipSwitchedOff_NeverSponsorable()
        {
            var s = Compute(new[] { Entry(1, 30, DirectoryStatus.Verified, 365) });

            Assert.Empty(s.Subcategories);
            Assert.Empty(s.Categories);
        }

        [Fact]
        public void AdditionalSubcategory_CountsForThatSubcategoryOnly()
        {
            var s = Compute(
                new[] { Entry(1, 10, DirectoryStatus.Verified, 365) },
                pairs: new[] { (1, 20) });

            Assert.Contains(20, s.Subcategories);
            Assert.DoesNotContain(2, s.Categories); // category placement follows the main subcategory
        }

        [Fact]
        public void AdditionalSubcategory_OfANonQualifyingListing_DoesNotCount()
        {
            var s = Compute(
                new[] { Entry(1, 10, DirectoryStatus.Verified, 30) },
                pairs: new[] { (1, 20) });

            Assert.DoesNotContain(20, s.Subcategories);
        }

        private static SponsorableScopes Compute(
            IEnumerable<DirectoryEntry> entries,
            IEnumerable<(int, int)>? pairs = null,
            HashSet<int>? active = null) =>
            SponsorableScopeService.Compute(entries, pairs ?? Array.Empty<(int, int)>(), Subs, active ?? new HashSet<int>(), MinDays, Now);

        private static DirectoryEntry Entry(int id, int subId, DirectoryStatus status, int ageDays) => new ()
        {
            DirectoryEntryId = id,
            Name = $"Entry {id}",
            DirectoryEntryKey = $"entry-{id}",
            Link = "https://example.com",
            SubCategoryId = subId,
            DirectoryStatus = status,
            CreateDate = Now.AddDays(-ageDays),
        };
    }
}
