using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.AffiliateCommissionPaid
{
    public class AffiliateCommissionEarnedListViewModel
    {
        public List<AffiliateCommissionEarned> Items { get; set; } = new ();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalUsdValue { get; set; }

        public int? DirectoryEntryId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// The currently selected quick date-range preset (e.g. "currentyear", "last1year"),
        /// or null/empty for a custom range. Drives the no-JS quick-range dropdown selection.
        /// </summary>
        public string? QuickRange { get; set; }

        /// <summary>
        /// Sum of the amount actually received, broken out per payment currency, for the
        /// current filter. Shown in parenthesis next to the USD total.
        /// </summary>
        public IReadOnlyDictionary<Currency, decimal> CurrencyTotals { get; set; }
            = new Dictionary<Currency, decimal>();

        public List<SelectListItem> DirectoryEntries { get; set; } = new ();

        public int TotalPages =>
            this.PageSize > 0 ? (int)Math.Ceiling((double)this.TotalCount / this.PageSize) : 0;

        public bool HasPrevious => this.Page > 1;
        public bool HasNext => this.Page < this.TotalPages;
    }
}