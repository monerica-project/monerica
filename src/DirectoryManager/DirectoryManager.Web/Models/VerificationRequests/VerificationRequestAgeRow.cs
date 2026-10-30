using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.VerificationRequests
{
    /// <summary>One listing with pending verification request(s), for the oldest-listings view.</summary>
    public class VerificationRequestAgeRow
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string DirectoryEntryKey { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public DirectoryStatus Status { get; set; }

        /// <summary>When the listing was added to the directory.</summary>
        public DateTime ListedOn { get; set; }

        public int DaysListed { get; set; }

        /// <summary>First day it can buy a sponsorship once Verified (null = already can).</summary>
        public DateTime? CanSponsorFrom { get; set; }

        public int RequestCount { get; set; }

        public DateTime LatestRequest { get; set; }

        public int LatestRequestId { get; set; }

        public string LatestComment { get; set; } = string.Empty;

        public bool AnyDonation { get; set; }
    }
}
