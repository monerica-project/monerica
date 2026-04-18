using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipPricingSummaryVm
    {
        public SponsorshipType SponsorshipType { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal MinPriceUsd { get; set; }
        public decimal MaxPriceUsd { get; set; }
        public int MinDays { get; set; }
        public int MaxDays { get; set; }
        public decimal MinUsdPerDay { get; set; }
        public decimal MaxUsdPerDay { get; set; }
    }
}