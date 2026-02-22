using System.ComponentModel.DataAnnotations;
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

        // Concurrency
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<DirectoryEntryReviewComment> Comments { get; set; } = new List<DirectoryEntryReviewComment>();

        public ICollection<DirectoryEntryReviewTag> ReviewTags { get; set; } = new List<DirectoryEntryReviewTag>();

    }
}