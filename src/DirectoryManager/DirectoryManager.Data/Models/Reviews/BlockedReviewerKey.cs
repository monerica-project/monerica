using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models.Reviews
{
    /// <summary>
    /// A PGP key (fingerprint) an admin blocked from posting reviews and replies. One row per
    /// fingerprint: unblocking keeps the row (with <see cref="UnblockedUtc"/> set) as an audit
    /// trail, and blocking the same key again reuses it. Admin-only data; no IP addresses.
    /// </summary>
    public class BlockedReviewerKey
    {
        [Key]
        public int BlockedReviewerKeyId { get; set; }

        /// <summary>Full fingerprint, uppercase hex, no spaces (the form posts store it in).</summary>
        [Required]
        [MaxLength(64)]
        public string Fingerprint { get; set; } = string.Empty;

        /// <summary>Short admin-only reason. Never shown to the key holder.</summary>
        [MaxLength(200)]
        public string? Reason { get; set; }

        public DateTime BlockedUtc { get; set; }

        [MaxLength(256)]
        public string? BlockedBy { get; set; }

        /// <summary>True when the block also hid the key's existing posts.</summary>
        public bool HidPosts { get; set; }

        /// <summary>Null while the block is in force.</summary>
        public DateTime? UnblockedUtc { get; set; }

        [MaxLength(256)]
        public string? UnblockedBy { get; set; }
    }
}
