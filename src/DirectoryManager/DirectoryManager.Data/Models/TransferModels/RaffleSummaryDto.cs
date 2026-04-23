using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>
    /// Lightweight projection of a Raffle plus aggregated entry counts
    /// for the admin index page.
    /// </summary>
    public class RaffleSummaryDto
    {
        public int RaffleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsEnabled { get; set; }

        public int TotalEntries { get; set; }
        public int PendingCount { get; set; }
        public int EligibleCount { get; set; }
        public int PaidCount { get; set; }
        public int DisqualifiedCount { get; set; }
        public int EndedCount { get; set; }

        public int CountFor(RaffleEntryStatus s) => s switch
        {
            RaffleEntryStatus.Pending => this.PendingCount,
            RaffleEntryStatus.Eligible => this.EligibleCount,
            RaffleEntryStatus.Paid => this.PaidCount,
            RaffleEntryStatus.Disqualified => this.DisqualifiedCount,
            RaffleEntryStatus.Ended => this.EndedCount,
            _ => 0
        };
    }
}
