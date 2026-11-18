using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Web.Helpers;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>A review's author adjusting their own rating/tags through the reply flow.</summary>
    public class OwnReviewEditTests
    {
        // 1 Valid Order (admin-only), 6 Suspicious Review (admin-only), 14 Misleading fees, 15 Funds held,
        // 10 $1K-5K and 2 Under $50 (money bands), 99 disabled tag is simply absent from "enabled".
        private static readonly List<ReviewTag> Tags = new ()
        {
            new () { ReviewTagId = 1, Name = "Valid Order", ReviewerSelectable = false },
            new () { ReviewTagId = 6, Name = "Suspicious Review", ReviewerSelectable = false },
            new () { ReviewTagId = 14, Name = "Misleading fees" },
            new () { ReviewTagId = 15, Name = "Funds held" },
            new () { ReviewTagId = 10, Name = "$1K-5K", MinUsd = 1000m, MaxUsd = 5000m },
            new () { ReviewTagId = 2, Name = "Under $50", MaxUsd = 50m },
        };

        [Theory] // rating: only 1–5 and only when different
        [InlineData((byte)1, (byte)3, (byte)3)]
        [InlineData((byte)1, (byte)1, null)]
        [InlineData((byte)1, (byte)0, null)]
        [InlineData((byte)1, (byte)6, null)]
        [InlineData(null, (byte)4, (byte)4)]
        public void RatingChange_Rules(byte? current, byte? requested, byte? expected)
        {
            Assert.Equal(expected, OwnReviewEdit.RatingChange(current, requested));
        }

        [Fact] // SAD: admin-only and unknown tags can't be self-applied; checkbox money bands are ignored
        public void RequestedTags_OnlyPublicTags_OneMoneyBand()
        {
            var requested = OwnReviewEdit.RequestedTags(Tags, new[] { 1, 6, 14, 14, 99, 2 }, moneyBandTagId: 10);

            Assert.Equal(new[] { 10, 14 }, requested);
        }

        [Fact] // HAPPY: approval keeps admin-only tags and replaces the public ones with the request
        public void ApplyRequested_KeepsAdminOnlyTags()
        {
            var current = new[] { 1, 14, 15, 10 };
            var updated = OwnReviewEdit.ApplyRequested(current, Tags, new[] { 15, 2 });

            Assert.Equal(new[] { 1, 2, 15 }, updated);
        }

        [Fact] // HAPPY: "no change" is detected, so an unchanged form never sends the reply to moderation
        public void Unchanged_TagsCompareEqual()
        {
            var current = OwnReviewEdit.CurrentPublicTags(new[] { 15, 1, 14, 10 }, Tags);
            var requested = OwnReviewEdit.RequestedTags(Tags, new[] { 14, 15 }, 10);

            Assert.True(requested.SequenceEqual(current));
        }

        [Fact] // CSV round-trip, including "remove all" (empty string)
        public void Csv_RoundTrip()
        {
            Assert.Equal(new[] { 2, 15 }, OwnReviewEdit.FromCsv(OwnReviewEdit.ToCsv(new[] { 15, 2 })));
            Assert.Empty(OwnReviewEdit.FromCsv(string.Empty));
        }
    }
}
