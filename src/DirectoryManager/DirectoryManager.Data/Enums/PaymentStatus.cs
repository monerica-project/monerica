using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    public enum PaymentStatus
    {
        Unknown = 0,
        [Description("Invoice Created")]
        InvoiceCreated = 1,
        Pending = 2,
        [Description("Under Payment")]
        UnderPayment = 3,
        [Description("Over Payment")]
        OverPayment = 4,
        Paid = 5,
        Expired = 6,
        Failed = 7,
        Test = 8,
        Canceled = 9,
    }
}