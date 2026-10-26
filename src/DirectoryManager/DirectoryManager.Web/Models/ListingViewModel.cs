using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class ListingViewModel
    {
        public int Id { get; set; }
        required public string DirectoryEntryName { get; set; }
        public SponsorshipType SponsorshipType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>The placement's id for the waitlist (category or subcategory id; null for main).</summary>
        public int? WaitlistTypeId { get; set; }

        /// <summary>How many listings are waiting for this placement to open.</summary>
        public int WaitlistCount { get; set; }

        public bool IsActive
        {
            get
            {
                DateTime currentDate = DateTime.UtcNow;
                return this.StartDate <= currentDate && this.EndDate >= currentDate;
            }
        }
    }
}