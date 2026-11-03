namespace DirectoryManager.Web.Models.Revenue
{
    /// <summary>A sponsor whose time is running out, or recently ran out.</summary>
    public class RenewalRow
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ProfilePath { get; set; }

        public string Placement { get; set; } = string.Empty;

        public DateTime EndsUtc { get; set; }

        public int DaysLeft { get; set; }

        public int TimesBought { get; set; }

        public decimal TotalPaidUsd { get; set; }

        public string? Email { get; set; }

        public int WaitlistCount { get; set; }

        public bool HasOtherActiveSponsorship { get; set; }
    }
}
