namespace DirectoryManager.Web.Models.Revenue
{
    /// <summary>Counts shown next to the revenue links on the account home page.</summary>
    public class RevenueSummary
    {
        public int AbandonedLast30Days { get; set; }

        public int ExpiringIn30Days { get; set; }

        public int Lapsed { get; set; }

        public int Prospects { get; set; }

        public int InterestLast7Days { get; set; }
    }
}
