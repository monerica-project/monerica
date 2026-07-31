using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    /// <summary>
    /// Outbox row: one per owner email notification for a newly-approved review or reply.
    /// Guaranteed once-only per review/comment via filtered unique indexes. The poller
    /// enqueues Pending rows and sends them, so approval and delivery are decoupled.
    /// </summary>
    public class ReviewNotification : StateInfo
    {
        public int ReviewNotificationId { get; set; }

        public int DirectoryEntryId { get; set; }

        public DirectoryEntry? DirectoryEntry { get; set; }

        public ReviewNotificationType NotificationType { get; set; }

        // Exactly one of these is set, depending on NotificationType.
        public int? DirectoryEntryReviewId { get; set; }

        public int? DirectoryEntryReviewCommentId { get; set; }

        [MaxLength(255)]
        public string RecipientEmail { get; set; } = string.Empty;

        public ReviewNotificationStatus Status { get; set; } = ReviewNotificationStatus.Pending;

        public int AttemptCount { get; set; }

        public DateTime? SentUtc { get; set; }

        [MaxLength(1000)]
        public string? LastError { get; set; }
    }
}
