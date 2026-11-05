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

        [Theory]
        [InlineData(SponsorInterestStep.ViewedOptions, false)]
        [InlineData(SponsorInterestStep.StartedPayment, false)]
        [InlineData(SponsorInterestStep.VideoViewedPrices, true)]
        [InlineData(SponsorInterestStep.VideoStartedPayment, true)]
        public void VideoSteps_AreKeptApartFromSiteSteps(SponsorInterestStep step, bool isVideo) => Assert.Equal(isVideo, step.IsVideo());

        [Fact]
        public void StartedPayment_CoversSiteAndVideo()
        {
            Assert.True(SponsorInterestStep.StartedPayment.IsStartedPayment());
            Assert.True(SponsorInterestStep.VideoStartedPayment.IsStartedPayment());
            Assert.False(SponsorInterestStep.VideoReviewedOrder.IsStartedPayment());
            Assert.True(SponsorInterestStep.VideoStartedPayment > SponsorInterestStep.VideoReviewedOrder);
            Assert.Equal("Started payment", SponsorInterestController.StepLabel(SponsorInterestStep.VideoStartedPayment));
            Assert.Equal("Picking videos", SponsorInterestController.StepLabel(SponsorInterestStep.VideoPickedVideos));
        }

        [Theory]
        [InlineData("image", "same-origin", true)]
        [InlineData(null, null, true)]                 // older browsers send no Sec-Fetch headers
        [InlineData("document", "none", false)]        // crawler fetching the beacon URL directly
        [InlineData("image", "cross-site", false)]     // hotlinked from another site
        [InlineData("empty", "same-origin", false)]    // fetch()/XHR, not an image load
        public void Beacon_OnlyCountsRealImageLoads(string? dest, string? site, bool counted) =>
            Assert.Equal(counted, SponsorInterestTracker.LooksLikeImageLoad(dest, site));

        [Fact]
        public void Beacon_IsUsedForPageViews_NotForPayments()
        {
            Assert.True(SponsorInterestTracker.IsBeaconStep(SponsorInterestStep.ViewedOptions));
            Assert.True(SponsorInterestTracker.IsBeaconStep(SponsorInterestStep.VideoReviewedOrder));
            Assert.False(SponsorInterestTracker.IsBeaconStep(SponsorInterestStep.StartedPayment));
            Assert.False(SponsorInterestTracker.IsBeaconStep(SponsorInterestStep.VideoStartedPayment));
        }

        [Fact]
        public void Beacon_UrlCarriesListingTypeStepAndScope()
        {
            var vd = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
            SponsorInterestTracker.SetBeacon(vd, 42, SponsorshipType.CategorySponsor, 7, SponsorInterestStep.ViewedPrices);
            Assert.Equal("/sponsor-interest/seen.gif?e=42&t=3&s=2&p=7", vd[SponsorInterestTracker.BeaconViewDataKey]);

            var vd2 = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
            SponsorInterestTracker.SetBeacon(vd2, 42, SponsorshipType.Unknown, null, SponsorInterestStep.StartedPayment);
            Assert.Null(vd2[SponsorInterestTracker.BeaconViewDataKey]);
        }

        [Fact]
        public void Scope_KeepsCategoryAndVideoOfferIds_Only()
        {
            Assert.Equal(7, SponsorInterestTracker.ScopeFor(SponsorshipType.CategorySponsor, 7, SponsorInterestStep.ViewedPrices));
            Assert.Null(SponsorInterestTracker.ScopeFor(SponsorshipType.MainSponsor, 7, SponsorInterestStep.ViewedPrices));
            Assert.Equal(2, SponsorInterestTracker.ScopeFor(SponsorshipType.Unknown, 2, SponsorInterestStep.VideoReviewedOrder));
            Assert.Null(SponsorInterestTracker.ScopeFor(SponsorshipType.Unknown, null, SponsorInterestStep.VideoViewedPrices));
            Assert.Null(SponsorInterestTracker.ScopeFor(SponsorshipType.Unknown, 0, SponsorInterestStep.VideoPickedVideos));
        }
    }
}
