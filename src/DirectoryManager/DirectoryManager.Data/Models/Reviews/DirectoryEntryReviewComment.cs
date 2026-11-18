using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

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

        // Keep consistent with review's invariant: uppercase, no spaces (normalize before saving)
        [Required]
        [MaxLength(64)]
        public string AuthorFingerprint { get; set; } = string.Empty;

        // Member identity, same meaning as on DirectoryEntryReview. Null for PGP replies.
        [MaxLength(36)]
        public string? AuthorUserId { get; set; }

        [MaxLength(32)]
        public string? AuthorUsername { get; set; }

        // Optional navigation for nested children
        [InverseProperty(nameof(ParentComment))]
        public ICollection<DirectoryEntryReviewComment>? Children { get; set; }

        /// <summary>
        /// Set when the review's own AUTHOR changed the review's star rating while posting this reply
        /// (the rating changes immediately; the written review never does). Shown on the reply.
        /// </summary>
        public byte? RatingChangedFrom { get; set; }

        public byte? RatingChangedTo { get; set; }

        /// <summary>
        /// The review author's requested set of public tags, as a CSV of tag ids ("" = remove all
        /// public tags), sent with this reply. Applied to the review only when a moderator approves
        /// the reply; admin-only tags are always kept. Null = no tag change requested.
        /// </summary>
        [MaxLength(200)]
        public string? RequestedReviewTagIdsCsv { get; set; }

        /// <summary>When the requested tag change was applied (on approval).</summary>
        public DateTime? RequestedTagsAppliedUtc { get; set; }

        [NotMapped]
        public string? DisplayName { get; set; }

        // True when this reply's AuthorFingerprint matches the listing's verified
        // PGP key (entry.PgpKey). Set by the presentation layer so the UI can
        // visibly mark replies that come from the actual site/listing owner.
        [NotMapped]
        public bool IsOwner { get; set; }

        // True when this reply's AuthorFingerprint matches the DIRECTORY's own PGP key
        // (SiteConfigSetting.PgpKey). Set by the presentation layer so the UI can label it
        // as an official "Moderator" response (distinct from a listing-owner reply).
        [NotMapped]
        public bool IsModerator { get; set; }
    }
}