using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    /// <summary>
    /// Generic, append-only audit log of every email handed to SendGrid by any
    /// service (newsletter sender, sponsored-listing jobs, the web app, etc.).
    /// Unlike <see cref="SentEmailRecord"/> — which is scoped to newsletter
    /// campaigns and used for de-duplication — this table records ALL outbound
    /// mail across the whole system, successful or not.
    /// </summary>
    public class EmailSendLog : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailSendLogId { get; set; }

        /// <summary>
        /// Which service/application sent the message (see EmailSendSource).
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SourceApplication { get; set; } = string.Empty;

        /// <summary>
        /// Primary recipient. For batch (BCC) sends this is the first address;
        /// <see cref="RecipientCount"/> reflects the full size of the batch.
        /// </summary>
        [Required]
        [MaxLength(320)]
        public string RecipientEmail { get; set; } = string.Empty;

        /// <summary>
        /// Number of recipients on this single send (1 for the common case).
        /// </summary>
        public int RecipientCount { get; set; } = 1;

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// True when SendGrid accepted the message (HTTP 2xx).
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// SendGrid HTTP status code (202 on success). Null if the send threw
        /// before a response was received.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// SendGrid response body or exception message when the send failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        public DateTime SentDate { get; set; } = DateTime.UtcNow;
    }
}
