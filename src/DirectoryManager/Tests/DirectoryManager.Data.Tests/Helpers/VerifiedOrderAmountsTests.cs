using DirectoryManager.Data.Helpers;

namespace DirectoryManager.Data.Tests.Helpers
{
    public class VerifiedOrderAmountsTests
    {
        [Fact]
        public void Summarize_UsesBandMidpointsAndOpenEnds()
        {
            var (count, avg) = VerifiedOrderAmounts.Summarize(new (decimal?, decimal?)[]
            {
                (1000m, 5000m),   // 3000
                (null, 50m),      // under $50 -> 25
                (100000m, null),  // over $100K -> 100000
                (null, null),     // not a money band -> ignored
            });
            Assert.Equal(3, count);
            Assert.Equal((3000m + 25m + 100000m) / 3m, avg);
        }

        [Fact]
        public void Summarize_EmptyHasNoAverage() =>
            Assert.Equal((0, (decimal?)null), VerifiedOrderAmounts.Summarize(Array.Empty<(decimal?, decimal?)>()));

        [Theory]
        [InlineData(25.0, "Under $50")]
        [InlineData(66.0, "$50-200")]
        [InlineData(2320.0, "$1K-5K")]
        [InlineData(19096.0, "$10K-20K")]
        [InlineData(20000.0, "$20K-50K")]
        [InlineData(250000.0, "Over $100K")]
        public void Bucket_MatchesTheReviewTagRanges(double usd, string expected) =>
            Assert.Equal(expected, VerifiedOrderAmounts.Bucket((decimal)usd));

        [Theory]
        [InlineData(66.4, "$66")]
        [InlineData(2318.0, "$2,320")]
        [InlineData(19096.0, "$19,100")]
        public void Format_RoundsSensibly(double usd, string expected) =>
            Assert.Equal(expected, VerifiedOrderAmounts.Format((decimal)usd));
    }
}
