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

        public int? TypeId { get; set; }

        public bool IsReminderSent { get; set; }

        // Timestamp to track when the user subscribed
        public DateTime SubscribedDate { get; set; } = DateTime.UtcNow;
    }
}