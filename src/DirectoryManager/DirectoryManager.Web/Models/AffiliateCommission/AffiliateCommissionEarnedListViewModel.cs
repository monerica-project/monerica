using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

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

        public List<SelectListItem> DirectoryEntries { get; set; } = new ();

        public int TotalPages =>
            this.PageSize > 0 ? (int)Math.Ceiling((double)this.TotalCount / this.PageSize) : 0;

        public bool HasPrevious => this.Page > 1;
        public bool HasNext => this.Page < this.TotalPages;
    }
}