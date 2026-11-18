using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Models.Reviews
{
    /// <summary>
    /// Admin-only record of a review or reply that was deleted, written in the same transaction as
    /// the delete. The public row is really gone (so nothing public can show it), but the owner can
    /// still see that it existed, who deleted it and when. Never shown publicly.
    /// Deleted by its AUTHOR: the author's text and proof are not kept (Body is empty); only the
    /// facts and other people's replies remain, and it cannot be restored.
    /// Deleted by an ADMIN: a full copy is kept and the review can be restored.
    /// </summary>
    [Index(nameof(DeletedUtc))]
    public class DeletedReviewRecord
    {
        public const string KindReview = "review";
        public const string KindReply = "reply";

        public const string ByAuthor = "author";
        public const string ByAdmin = "admin";

        public int Id { get; set; }

        /// <summary>"review" or "reply".</summary>
        [MaxLength(10)]
        public string Kind { get; set; } = KindReview;

        /// <summary>Id the review/reply had before it was deleted.</summary>
        public int OriginalId { get; set; }

        /// <summary>For a reply: the review it was under.</summary>
        public int? ParentReviewId { get; set; }

        public int DirectoryEntryId { get; set; }

        [MaxLength(255)]
        public string? ListingName { get; set; }

        /// <summary>"author" (the person who wrote it) or "admin".</summary>
        [MaxLength(10)]
        public string DeletedBy { get; set; } = ByAdmin;

        public DateTime DeletedUtc { get; set; }

        public DateTime PostedUtc { get; set; }

        [MaxLength(36)]
        public string? AuthorUserId { get; set; }

        [MaxLength(32)]
        public string? AuthorUsername { get; set; }

        [MaxLength(40)]
        public string? AuthorFingerprint { get; set; }

        public byte? Rating { get; set; }

        /// <summary>Moderation status it had when deleted (Approved, Pending, ...).</summary>
        [MaxLength(20)]
        public string? StatusAtDelete { get; set; }

        public string Body { get; set; } = string.Empty;

        /// <summary>Replies that were removed together with a deleted review.</summary>
        public int ReplyCount { get; set; }

        /// <summary>Full copy (review fields, tag ids, replies) used to show and restore it.</summary>
        public string? SnapshotJson { get; set; }

        public DateTime? RestoredUtc { get; set; }

        /// <summary>Id of the review created by a restore.</summary>
        public int? RestoredAsId { get; set; }
    }
}
