using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.Notifications
{
    public class Notification
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public NotificationType NotificationType { get; set; }
        public DateTime? ExpirationDate { get; set; } // Used for expiration-based notifications
        public SponsorshipType? SponsorType { get; set; } // For sponsor notifications
        public bool IsSent { get; set; } = false;
    }
}
