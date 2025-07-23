using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
