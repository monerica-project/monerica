using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.Notifications
{
    public class AdSpot
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public SponsorshipType SponsorType { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool IsAvailable => DateTime.UtcNow > this.ExpirationDate;
    }
}
