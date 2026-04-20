using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.AffiliateCommissionPaid
{

    public class AffiliateCommissionEarnedEditViewModel
    {
        public int AffiliateCommissionEarnedId { get; set; }

        [Required]
        [Display(Name = "Directory Entry")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a directory entry.")]
        public int DirectoryEntryId { get; set; }

        [Required]
        [Display(Name = "Commission Date (UTC)")]
        [DataType(DataType.DateTime)]
        public DateTime CommissionDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "USD Value")]
        [Range(0.00, 1_000_000_000.00)]
        [DataType(DataType.Currency)]
        public decimal UsdValue { get; set; }

        [Required]
        [Display(Name = "Payment Currency")]
        public Currency PaymentCurrency { get; set; }

        [Required]
        [Display(Name = "Payment Currency Amount")]
        [Range(0.00000000, 1_000_000_000.00000000)]
        public decimal PaymentCurrencyAmount { get; set; }

        [MaxLength(255)]
        [Display(Name = "Transaction ID (optional)")]
        public string? TransactionId { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Note (optional)")]
        public string? Note { get; set; }

        public List<SelectListItem> DirectoryEntries { get; set; } = new ();
        public List<SelectListItem> CurrencyOptions { get; set; } = new ();
    }
}