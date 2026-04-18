using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    public class InvoiceTotalsResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalReceivedAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public Currency Currency { get; set; } = Currency.Unknown;
        public Currency PaidInCurrency { get; set; } = Currency.Unknown;
    }
}