using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.Sponsorship
{
    /// <summary>
    /// Backing model for the PUBLIC sponsorship pricing-history explorer. Non-authenticated visitors
    /// can pick a placement (all, main, every category page, one category page, every subcategory page
    /// or one subcategory page) and a date range (capped to the last 180 days) and see
    /// what advertisers have actually paid.
    /// </summary>
    public class PublicPricingViewModel
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        /// <summary>Selected preset window in days (30 / 60 / 90 / 180). Never exceeds 180.</summary>
        public int RangeDays { get; set; } = 180;

        public List<SelectListItem> RangeOptions { get; set; } = new ();

        /// <summary>null = compare all placement types in one chart; otherwise a single type.</summary>
        public SponsorshipType? SponsorshipType { get; set; }

        /// <summary>One category page (category sponsor) or subcategory page (subcategory sponsor).</summary>
        public int? PlacementId { get; set; }

        /// <summary>Picker value: "", "main", "category", "subcategory", "category-8", "subcategory-73".</summary>
        public string Placement { get; set; } = string.Empty;

        public string? PlacementLabel { get; set; }

        public List<SelectListItem> PlacementOptions { get; set; } = new ();

        public int WindowDays { get; set; } = 180;
    }
}
