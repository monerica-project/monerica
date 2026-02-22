using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirectoryManager.Data.Models.Reviews
{
    public class DirectoryEntryReviewComment : UserStateInfo
    {
        [Key]
        public int DirectoryEntryReviewCommentId { get; set; }

        // FK to the review this comment is attached to
        [Required]
        public int DirectoryEntryReviewId { get; set; }

        [ForeignKey(nameof(DirectoryEntryReviewId))]
        public DirectoryEntryReview? DirectoryEntryReview { get; set; }

        // Optional nested threading (reply-to-comment). Leave null for top-level comments under the review.
        public int? ParentCommentId { get; set; }

        [ForeignKey(nameof(ParentCommentId))]
        public DirectoryEntryReviewComment? ParentComment { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;

        // Same moderation semantics as reviews (keeps your admin queue simple)
        public ReviewModerationStatus ModerationStatus { get; set; } = ReviewModerationStatus.Pending;

        [MaxLength(800)]
        public string? RejectionReason { get; set; }

        // Keep consistent with review’s invariant: uppercase, no spaces (normalize before saving)
        [Required]
        [MaxLength(64)]
        public string AuthorFingerprint { get; set; } = string.Empty;

        // Optional navigation for nested children
        public ICollection<DirectoryEntryReviewComment>? Children { get; set; }

        public ICollection<DirectoryEntryReviewComment>? Comments { get; set; }
        [NotMapped]
        public string? DisplayName { get; set; }
    }
}