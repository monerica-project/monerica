using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Models;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class LastReviewInfoTests
    {
        [Theory]
        [InlineData(ReviewModerationStatus.Approved, AutoModerationResult.AutoApproved, "auto-approved")]
        [InlineData(ReviewModerationStatus.Approved, AutoModerationResult.None, "approved")]
        [InlineData(ReviewModerationStatus.Pending, AutoModerationResult.None, "pending")]
        [InlineData(ReviewModerationStatus.Flagged, AutoModerationResult.Flagged, "flagged")]
        [InlineData(ReviewModerationStatus.Rejected, AutoModerationResult.AutoRejected, "rejected")]
        public void StatusLabel_SaysHowItWasHandled(ReviewModerationStatus status, AutoModerationResult auto, string expected)
            => Assert.Equal(expected, new LastReviewInfo { Status = status, AutoResult = auto }.StatusLabel);
    }
}
