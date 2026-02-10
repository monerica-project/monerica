using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingOpeningNotification : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SponsoredListingOpeningNotificationId { get; set; }

        [StringLength(100)]
        [Required]
        public string Email { get; set; } = string.Empty;

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;

        /// <summary>
        /// For Category/Subcategory sponsor this is the CategoryId/SubCategoryId (depending on SponsorshipType).
        /// Null for types that are global (ex: Main Sponsor).
        /// </summary>
        public int? TypeId { get; set; }

        /// <summary>
        /// The advertiser’s listing in your directory (optional but recommended so you can show FOMO queue).
        /// </summary>
        public int? DirectoryEntryId { get; set; }

        public bool IsReminderSent { get; set; }

        /// <summary>
        /// Treat as "last subscribed" (your repo refreshes this now).
        /// </summary>
        public DateTime SubscribedDate { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; }

        /// <summary>
        /// When the opening reminder email was sent (UTC).
        /// </summary>
        public DateTime? ReminderSentDateUtc { get; set; }

        /// <summary>
        /// The link that was emailed (useful for debugging/support).
        /// </summary>
        [StringLength(700)]
        public string? ReminderSentLink { get; set; }
    }
}
