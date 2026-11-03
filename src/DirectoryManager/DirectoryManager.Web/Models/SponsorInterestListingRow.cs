namespace DirectoryManager.Web.Models
{
    public class SponsorInterestListingRow
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ProfilePath { get; set; }

        public List<string> Placements { get; set; } = new ();

        public string FurthestStep { get; set; } = string.Empty;

        public int FurthestStepValue { get; set; }

        public int Looks { get; set; }

        public int Days { get; set; }

        public DateTime FirstSeen { get; set; }

        public DateTime LastSeen { get; set; }

        public DateTime? PaidDate { get; set; }
    }
}
