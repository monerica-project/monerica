using DirectoryManager.Data.Helpers;

namespace DirectoryManager.Data.Tests.Helpers
{
    public class ReviewSwapFactsTests
    {
        [Theory]
        [InlineData("exchanges", true)]
        [InlineData("Exchanges", true)]
        [InlineData(" exchanges ", true)]
        [InlineData("vpns", false)]
        [InlineData("exchanges-archive", false)]
        [InlineData(null, false)]
        public void AppliesOnlyToTheExchangesCategory(string? key, bool expected) =>
            Assert.Equal(expected, ReviewSwapFacts.AppliesToCategory(key));

        [Theory]
        [InlineData(null, null)]
        [InlineData(0, null)]
        [InlineData(-5, null)]
        [InlineData(1, 1)]
        [InlineData(43200, 43200)]
        [InlineData(43201, null)]
        public void MinutesOutOfRangeAreDropped(int? input, int? expected) =>
            Assert.Equal(expected, ReviewSwapFacts.CleanMinutes(input));

        [Theory]
        [InlineData(null, null)]
        [InlineData(-1, null)]
        [InlineData(0, 0)]
        [InlineData(10, 10)]
        [InlineData(1000, 1000)]
        [InlineData(1001, null)]
        public void ConfirmationsOutOfRangeAreDropped(int? input, int? expected) =>
            Assert.Equal(expected, ReviewSwapFacts.CleanConfirmations(input));

        [Theory]
        [InlineData(25, "25 min")]
        [InlineData(60, "1 h")]
        [InlineData(80, "1 h 20 min")]
        [InlineData(1440, "1 day")]
        [InlineData(1680, "1 day 4 h")]
        [InlineData(2880, "2 days")]
        [InlineData(0, null)]
        [InlineData(null, null)]
        public void DurationReadsNaturally(int? minutes, string? expected) =>
            Assert.Equal(expected, ReviewSwapFacts.FormatDuration(minutes));

        [Fact]
        public void SummaryShowsOnlyWhatWasStated()
        {
            Assert.Null(ReviewSwapFacts.Summary(null, null));
            Assert.Equal("Processing time: 25 min", ReviewSwapFacts.Summary(25, null));
            Assert.Equal("10 confirmations", ReviewSwapFacts.Summary(null, 10));
            Assert.Equal("1 confirmation", ReviewSwapFacts.Summary(null, 1));
            Assert.Equal("Processing time: 25 min · 10 confirmations", ReviewSwapFacts.Summary(25, 10));
            Assert.Null(ReviewSwapFacts.Summary(999999, -3)); // junk values never render
        }
    }
}
