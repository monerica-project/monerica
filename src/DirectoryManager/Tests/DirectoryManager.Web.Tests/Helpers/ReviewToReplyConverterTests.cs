using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Web.Helpers;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class ReviewToReplyConverterTests
    {
        private static DirectoryEntryReview Review(int id, int entryId, string body = "Thanks, it worked for me too.", string? title = null) => new ()
        {
            DirectoryEntryReviewId = id,
            DirectoryEntryId = entryId,
            Body = body,
            Title = title,
            AuthorFingerprint = "ABCDEF0123456789ABCDEF0123456789ABCDEF01",
            CreatedByUserId = "automated",
            Rating = 5,
        };

        [Fact]
        public void Build_CopiesTextAndAuthor_AsPendingReplyUnderTarget()
        {
            var src = Review(2035, 7);
            var reply = ReviewToReplyConverter.Build(src, 1990, "admin");

            Assert.Equal(1990, reply.DirectoryEntryReviewId);
            Assert.Null(reply.ParentCommentId);
            Assert.Equal(src.Body, reply.Body);
            Assert.Equal(src.AuthorFingerprint, reply.AuthorFingerprint);
            Assert.Equal(ReviewModerationStatus.Pending, reply.ModerationStatus);
        }

        [Theory]
        [InlineData("Great service", "It was fast.", "Great service\n\nIt was fast.")]
        [InlineData(null, "It was fast.", "It was fast.")]
        [InlineData("  ", "It was fast.", "It was fast.")]
        [InlineData("Great service", "", "Great service")]
        [InlineData("Great service", "Great service, it was fast.", "Great service, it was fast.")]
        public void BuildBody_PutsTitleOnFirstLine_WithoutDuplicating(string? title, string body, string expected) =>
            Assert.Equal(expected, ReviewToReplyConverter.BuildBody(Review(1, 1, body, title)));

        [Fact]
        public void Validate_RejectsSelfOtherListingMissingAndEmpty()
        {
            var src = Review(10, 7);
            Assert.NotNull(ReviewToReplyConverter.Validate(src, null));
            Assert.NotNull(ReviewToReplyConverter.Validate(src, Review(10, 7)));
            Assert.NotNull(ReviewToReplyConverter.Validate(src, Review(11, 8)));
            Assert.NotNull(ReviewToReplyConverter.Validate(Review(10, 7, body: " ", title: null), Review(11, 7)));
            Assert.Null(ReviewToReplyConverter.Validate(src, Review(11, 7)));
        }

        [Fact]
        public void RejectionReason_NamesTheTargetReview() =>
            Assert.StartsWith(ReviewToReplyConverter.MovedReasonPrefix + "1990", ReviewToReplyConverter.RejectionReason(1990));
    }
}
