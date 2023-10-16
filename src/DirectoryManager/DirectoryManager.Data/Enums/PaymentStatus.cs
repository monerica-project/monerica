namespace DirectoryManager.Data.Enums
{
    public enum PaymentStatus
    {
        Unknown = 0,
        InvoiceCreated = 1,
        Pending = 2,
        UnderPayment = 3,
        OverPayment = 4,
        Paid = 5,
        Expired = 6,
        Failed = 7
    }
}