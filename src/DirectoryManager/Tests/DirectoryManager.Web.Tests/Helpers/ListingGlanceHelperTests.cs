using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class ListingGlanceHelperTests
    {
        [Theory]
        [InlineData("1-2% fee | 24/7", null, "1–2%")]
        [InlineData("0.5% fee", null, "0.5%")]
        [InlineData("Fee: 0.4–0.8%", null, "0.4–0.8%")]
        [InlineData("fees from 1%", null, "1%")]
        [InlineData(null, "Swap with a 0.5% swap fee and no account.", "0.5%")]
        [InlineData("Guarantees No tainted coins, No logs", null, null)]
        [InlineData("10% Monero discount", null, null)]
        [InlineData(null, null, null)]
        public void StatedFee_ReadsTheFeeTheListingStates(string? note, string? description, string? expected) =>
            Assert.Equal(expected, ListingGlanceHelper.StatedFee(note, description));

        [Fact]
        public void OperatingSince_ShowsStartAndAge()
        {
            var now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
            Assert.Equal("Aug 2025 (1 yr)", ListingGlanceHelper.OperatingSince(new DateOnly(2025, 8, 21), now));
            Assert.Equal("Sep 2022 (4 yrs)", ListingGlanceHelper.OperatingSince(new DateOnly(2022, 9, 1), now));
            Assert.Equal("Mar 2026 (7 mo)", ListingGlanceHelper.OperatingSince(new DateOnly(2026, 3, 10), now));
            Assert.Equal("Oct 2026 (new)", ListingGlanceHelper.OperatingSince(new DateOnly(2026, 10, 20), now));
            Assert.Null(ListingGlanceHelper.OperatingSince(null, now));
            Assert.Null(ListingGlanceHelper.OperatingSince(new DateOnly(2027, 1, 1), now));
        }
    }
}
