using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    /// <summary>
    /// One address we may send marketing email to (e.g. sponsorship offers), with the private
    /// token used in its opt-out link. A row with <see cref="OptedOutUtc"/> set must never be
    /// sent marketing email again. Separate from the newsletter's EmailSubscription list.
    /// </summary>
    public class MarketingEmailContact : CreatedStateInfo
    {
        public const string SourceLink = "link";
        public const string SourceAdmin = "admin";

        [Key]
        public int MarketingEmailContactId { get; set; }

        /// <summary>Stored trimmed and lower-cased; unique.</summary>
        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Random 32-hex-character token that identifies this contact in opt-out links; unique.</summary>
        [Required]
        [MaxLength(64)]
        public string Token { get; set; } = string.Empty;

        public DateTime? OptedOutUtc { get; set; }

        /// <summary>How the opt-out happened: <see cref="SourceLink"/> or <see cref="SourceAdmin"/>.</summary>
        [MaxLength(20)]
        public string? OptOutSource { get; set; }

        [MaxLength(250)]
        public string? Note { get; set; }
    }
}
