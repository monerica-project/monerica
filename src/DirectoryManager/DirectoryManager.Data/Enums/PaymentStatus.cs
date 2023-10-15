namespace DirectoryManager.Data.Enums
{
    public enum PaymentStatus
    {
        Unknown = 0,
        Pending = 1,
        UnderPayment = 2,
        OverPayment = 3,
        Paid = 4,
        Expired = 5,
        Failed = 6
    }
}