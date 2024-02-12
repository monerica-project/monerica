using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class InvoiceQueryViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public Currency Currency { get; set; } = Currency.Unknown;
        public Currency PaidInCurrency { get; set; } = Currency.Unknown;
    }
}