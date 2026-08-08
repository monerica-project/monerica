using System;

namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>A current live sponsorship contributing to the recurring run-rate.</summary>
    public class ActiveSponsorRow
    {
        public string Advertiser { get; set; } = string.Empty;
        public string SponsorshipType { get; set; } = string.Empty;
        public DateTime EndsOn { get; set; }
        public decimal Amount { get; set; }
        public decimal MonthlyValue { get; set; }
    }
}
