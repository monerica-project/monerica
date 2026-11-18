using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    /// <summary>
    /// The content of a sent email (From, Reply-To, custom headers, plain-text and HTML body), stored
    /// once per DISTINCT message: <see cref="EmailSendLog"/> rows point at it, so a newsletter issue sent
    /// to many people is stored a single time. Per-recipient secrets (confirmation/unsubscribe tokens,
    /// invoice links) and the recipient's own address are replaced with placeholders before storing,
    /// which is also what makes otherwise-identical per-recipient messages hash the same.
    /// Rows not used for <c>EmailSendLog:ContentRetentionDays</c> days are deleted by a background job;
    /// the log rows stay and show "content no longer stored".
    /// </summary>
    public class EmailSendLogContent : CreatedStateInfo
    {
        public const int HashLength = 64;

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailSendLogContentId { get; set; }

        /// <summary>Lower-case hex SHA-256 of the (redacted, untruncated) content; unique.</summary>
        [Required]
        [MaxLength(HashLength)]
        public string ContentHash { get; set; } = string.Empty;

        [MaxLength(320)]
        public string FromEmail { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? FromName { get; set; }

        [MaxLength(320)]
        public string? ReplyTo { get; set; }

        /// <summary>Custom headers the sender added, one "Name: value" per line (redacted).</summary>
        public string? Headers { get; set; }

        public string? BodyText { get; set; }

        public string? BodyHtml { get; set; }

        public bool BodyTextTruncated { get; set; }

        public bool BodyHtmlTruncated { get; set; }

        /// <summary>UTF-8 size in bytes of the plain-text + HTML bodies as sent (before truncation).</summary>
        public int SizeBytes { get; set; }

        /// <summary>Last time a send used this content (retention is counted from here).</summary>
        public DateTime LastUsedUtc { get; set; }
    }
}
