using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models
{
    /// <summary>
    /// Backing model for the admin "Pricing Trends" report — the average per-day price advertisers
    /// actually PAID for a sponsorship slot, per placement type, over a date range, with an optional
    /// subcategory drill-down (only subcategories that have paid invoices are selectable).
    /// </summary>
    public class PricingTrendsViewModel
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        /// <summary>null = compare all placement types; otherwise a single placement type.</summary>
        public SponsorshipType? SponsorshipType { get; set; }

        public Currency DisplayCurrency { get; set; } = Currency.USD;

        /// <summary>null = every paying subcategory; otherwise restrict to this one.</summary>
        public int? SubCategoryId { get; set; }

        public List<SelectListItem> SponsorshipTypeOptions { get; set; } = new ();

        public List<SelectListItem> DisplayCurrencyOptions { get; set; } = new ();

        /// <summary>Only subcategories that appear on at least one PAID invoice.</summary>
        public List<SelectListItem> SubCategoryOptions { get; set; } = new ();

        /// <summary>Per-placement-type summary rows shown above the chart.</summary>
        public List<PricingTrendSummaryRow> Summary { get; set; } = new ();

        /// <summary>Suggested market price per placement type, derived from paid history.</summary>
        public List<MarketPriceRow> MarketPrices { get; set; } = new ();

        /// <summary>Number of trailing days used for the "recent" going-rate recommendation.</summary>
        public int RecentWindowDays { get; set; } = 90;

        /// <summary>Total distinct paying subcategories available for the filter.</summary>
        public int PayingSubcategoryCount { get; set; }

        public string? SubCategoryLabel { get; set; }

        public bool HasData => this.Summary.Any(r => r.DaysWithData > 0);
    }
}
