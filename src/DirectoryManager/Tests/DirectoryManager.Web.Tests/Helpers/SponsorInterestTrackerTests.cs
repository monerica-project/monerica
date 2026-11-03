using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class SponsorInterestTrackerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
        [InlineData("curl/8.5.0")]
        [InlineData("python-requests/2.31")]
        public void Bots_AreNotCounted(string? ua) => Assert.True(SponsorInterestTracker.IsBot(ua));

        [Theory]
        [InlineData("Mozilla/5.0 (Windows NT 10.0; rv:128.0) Gecko/20100101 Firefox/128.0")]
        [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1")]
        public void Browsers_AreCounted(string ua) => Assert.False(SponsorInterestTracker.IsBot(ua));

        [Fact]
        public void Weeks_StartOnMonday()
        {
            Assert.Equal(new DateTime(2026, 11, 2), SponsorInterestController.WeekStart(new DateTime(2026, 11, 3, 15, 0, 0)));
            Assert.Equal(new DateTime(2026, 11, 2), SponsorInterestController.WeekStart(new DateTime(2026, 11, 8, 23, 0, 0)));
        }

        [Fact]
        public void StartedPayment_IsTheFurthestStep()
        {
            Assert.True(SponsorInterestStep.StartedPayment > SponsorInterestStep.PickedDuration);
            Assert.Equal("Started payment", SponsorInterestController.StepLabel(SponsorInterestStep.StartedPayment));
        }
    }
}
