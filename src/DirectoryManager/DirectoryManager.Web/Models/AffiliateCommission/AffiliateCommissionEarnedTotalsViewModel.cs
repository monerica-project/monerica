using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.AffiliateCommissionPaid
{
    public class AffiliateCommissionEarnedTotalsViewModel
    {
        public List<AffiliateCommissionEarnedTotal> Totals { get; set; } = new ();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal GrandTotalUsd { get; set; }
    }
}