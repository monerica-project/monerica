using System;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>A sponsorship that was terminated before its paid-through date.</summary>
    public class CutShortRow
    {
        public string Advertiser { get; set; } = string.Empty;
        public DirectoryStatus Status { get; set; }
        public string SponsorshipType { get; set; } = string.Empty;
        public DateTime PaidThrough { get; set; }
        public DateTime ServedThrough { get; set; }
        public decimal Collected { get; set; }
        public decimal Unearned { get; set; }
    }
}
