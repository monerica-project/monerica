using System;
using System.Collections.Generic;
using System.Linq;
using DirectoryManager.Common.Constants;
using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models
{
    public class InvoiceQueryViewModel
    {
        public InvoiceQueryViewModel()
        {
            // Populate dropdown, excluding “Unknown”
            this.SponsorshipTypeOptions = Enum.GetValues(typeof(SponsorshipType))
                .Cast<SponsorshipType>()
                .Where(st => st != Data.Enums.SponsorshipType.Unknown)
                .Select(st => new SelectListItem
                {
                    Value = st.ToString(),
                    Text = st.ToString(),
                    Selected = false
                })
                .Prepend(new SelectListItem
                {
                    Value = "",
                    Text = "All",
                    Selected = true
                })
                .ToList();
        }

        public decimal FutureRevenueInDisplayCurrency { get; set; }
        public int FutureServiceDays { get; set; }
        public DateTime? PaidThroughDateUtc { get; set; }

        // Handy formatted string for Razor
        public string? PaidThroughDateYmd =>
            this.PaidThroughDateUtc?.ToUniversalTime().ToString(StringConstants.DateFormat);

        public Currency DisplayCurrency { get; set; } = Currency.USD;
        public List<SelectListItem> DisplayCurrencyOptions { get; set; } = new ();
        public decimal TotalInDisplayCurrency { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalPaidAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public Currency Currency { get; set; } = Currency.Unknown;

        public Currency PaidInCurrency { get; set; } = Currency.Unknown;

        /// <summary>
        /// null = All; otherwise filter by this specific type.
        /// </summary>
        public SponsorshipType? SponsorshipType { get; set; }

        /// <summary>
        /// Populated with “All” + each enum value (excluding Unknown).
        /// </summary>
        public List<SelectListItem> SponsorshipTypeOptions { get; set; }

        // DISTINCT future service days (union of remaining campaign days)
        public int FutureServiceDaysDistinct { get; set; }

        // Simple continuous days (today → latest end), may overcount if there are gaps
        public int FutureServiceDaysContinuous { get; set; }

        // NEW: Average FX shown when DisplayCurrency != USD
        public decimal? AverageUsdPerUnitForDisplayCurrency { get; set; }

        // NEW: Subcategory filter
        public int? SubCategoryId { get; set; }
        public List<SelectListItem> SubCategoryOptions { get; set; } = new ();

        public decimal? AverageUsdPerXmr { get; set; }
        public decimal? ImpliedTotalInXmrAtAvg { get; set; }
    }
}