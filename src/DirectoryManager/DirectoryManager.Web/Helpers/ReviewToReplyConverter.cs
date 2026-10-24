using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Turns a review that was posted in the wrong place into a reply under another review
    /// of the same listing (admin "Move to reply"). The reply keeps the author's fingerprint
    /// and text, and starts as Pending so it still goes through approval.
    /// </summary>
    public static class ReviewToReplyConverter
    {
        public const string MovedReasonPrefix = "Moved to a reply under review #";

        /// <summary>Checks the move is allowed.</summary>
        /// <returns>An error message, or null when the move is valid.</returns>
        public static string? Validate(DirectoryEntryReview source, DirectoryEntryReview? target)
        {
            if (target is null)
            {
                return "Choose the review to reply under.";
            }

            if (target.DirectoryEntryReviewId == source.DirectoryEntryReviewId)
            {
                return "A review can't be moved under itself.";
            }

            if (target.DirectoryEntryId != source.DirectoryEntryId)
            {
                return "The target review belongs to a different listing.";
            }

            if (string.IsNullOrWhiteSpace(source.Body) && string.IsNullOrWhiteSpace(source.Title))
            {
                return "This review has no text to move.";
            }

            return null;
        }

        /// <summary>The reply body: the review's title (if any) as the first line, then its text.</summary>
        /// <returns>The reply text.</returns>
        public static string BuildBody(DirectoryEntryReview source)
        {
            var title = (source.Title ?? string.Empty).Trim();
            var body = (source.Body ?? string.Empty).Trim();
            if (title.Length == 0)
            {
                return body;
            }

            return body.Length == 0 || body.StartsWith(title, StringComparison.OrdinalIgnoreCase) ? (body.Length == 0 ? title : body) : $"{title}\n\n{body}";
        }

        public static DirectoryEntryReviewComment Build(DirectoryEntryReview source, int targetReviewId, string adminUserId) => new ()
        {
            DirectoryEntryReviewId = targetReviewId,
            ParentCommentId = null,
            Body = BuildBody(source),
            AuthorFingerprint = source.AuthorFingerprint,
            ModerationStatus = ReviewModerationStatus.Pending,
            CreateDate = source.CreateDate,
            CreatedByUserId = string.IsNullOrWhiteSpace(source.CreatedByUserId) ? adminUserId : source.CreatedByUserId,
            UpdatedByUserId = adminUserId,
        };

        public static string RejectionReason(int targetReviewId) => $"{MovedReasonPrefix}{targetReviewId} (posted as a review by mistake).";
    }
}
