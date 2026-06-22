﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    public class DirectoryEntryReview : UserStateInfo
    {
        [Key]
        public int DirectoryEntryReviewId { get; set; }

        // FK → DirectoryEntry
        [Required]
        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        // PGP identity
        // Full primary key fingerprint (40 hex chars). Store canonical UPPERCASE/NO-SPACES.
        [Required]
        [MaxLength(40)]
        public string AuthorFingerprint { get; set; } = string.Empty;

        // Optional: armored public key you verified against (helps future re-verification)
        [Column(TypeName = "nvarchar(max)")]
        public string? AuthorPublicKeyArmor { get; set; }

        // Optional, user-friendly alias/handle derived from fingerprint (or signed claim)
        [MaxLength(64)]
        public string? AuthorHandle { get; set; } // e.g., bech32/“petname”

        // Optional, user-picked display name with a signed binding to fingerprint
        [MaxLength(64)]
        public string? DisplayName { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? DisplayNameSignatureArmor { get; set; } // clearsigned assertion blob

        // Review content
        [Range(1, 5)]
        public byte? Rating { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Body { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string RejectionReason { get; set; } = string.Empty;

        // Moderation & lifecycle
        public ReviewModerationStatus ModerationStatus { get; set; } = ReviewModerationStatus.Pending;

        // Minimal audit without storing sensitive data
        // Store SHA-256 hex (or base32) of the submission/deletion signatures for audit linking
        [MaxLength(64)]
        public string? PostSignatureHash { get; set; }

        [MaxLength(64)]
        public string? DeletionSignatureHash { get; set; }

        // Rate-limiting without raw IP: HMAC(IP) with server-secret pepper → hex
        [MaxLength(64)]
        public string? SourceIpHash { get; set; }

        [MaxLength(128)]
        public string? OrderId { get; set; }

        [MaxLength(2048)]
        public string? OrderUrl { get; set; }

        // Optional free-text supplied alongside the order-proof URL (e.g. a receiving
        // wallet address, an order reference, etc.). For crowd (non-official) reviews
        // this is internal/moderation use only and never shown publicly. For official
        // reviews it is surfaced publicly under an expandable "Verification details"
        // disclosure on the listing page, since official reviews are authored by
        // Monerica itself.
        [MaxLength(2048)]
        public string? OrderProofContext { get; set; }

        // =========================================================
        // Automated moderation audit
        //
        // Written by the DirectoryManager.ReviewModerator background job when it
        // evaluates an order-URL-bearing review. AutoModerationResult stays at None
        // for anything a human handled, so an automatic action is always
        // distinguishable from a manual one. AutoModerationReason captures the
        // machine-readable "why" (approved/rejected/flagged/retry). The attempt
        // counters drive back-off for orders that are still in progress.
        // =========================================================
        public AutoModerationResult AutoModerationResult { get; set; } = AutoModerationResult.None;

        [MaxLength(512)]
        public string? AutoModerationReason { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? AutoModeratedAtUtc { get; set; }

        public int AutoModerationAttemptCount { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? LastAutoModerationAttemptUtc { get; set; }

        // USD value the worker computed for the swap (deposit leg) at verification time.
        // Null until a price source is available; used to explain the money-band tag.
        [Column(TypeName = "decimal(18,2)")]
        public decimal? VerifiedOrderUsdValue { get; set; }

        // =========================================================
        // Official review
        //
        // When IsOfficial = true, this review is marked as an official
        // Monerica review: it is pinned above the user reviews and
        // badged accordingly. All substantive detail lives in the
        // review Body; the only extra structured fields are an optional
        // image link and the date it was tested/verified.
        // =========================================================
        public bool IsOfficial { get; set; }

        // Date the listing was tested/verified, stored as a UTC date (date-only).
        [Column(TypeName = "datetime2")]
        public DateTime? TestedAt { get; set; }

        // Optional link to an image (e.g. a screenshot) shown with the review.
        [MaxLength(2048)]
        public string? ImageUrl { get; set; }

        // Optional blockchain explorer links for the swap's send/receive legs.
        // Shown only on official reviews.
        [MaxLength(2048)]
        public string? SendingTxUrl { get; set; }

        [MaxLength(2048)]
        public string? ReceivingTxUrl { get; set; }

        // Optional link to a screenshot of an AML / coin-score check result
        // (e.g. an AMLBot transaction risk report) for the funds involved in
        // the swap. Shown only on official reviews.
        [MaxLength(2048)]
        public string? AmlScreenshotUrl { get; set; }

        // Concurrency
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // True when this review's AuthorFingerprint matches the listing's verified
        // PGP key (entry.PgpKey). Set by the presentation layer so the UI can
        // visibly mark reviews authored by the actual site/listing owner.
        [NotMapped]
        public bool IsOwner { get; set; }

        public ICollection<DirectoryEntryReviewComment> Comments { get; set; } = new List<DirectoryEntryReviewComment>();

        public ICollection<DirectoryEntryReviewTag> ReviewTags { get; set; } = new List<DirectoryEntryReviewTag>();
    }
}