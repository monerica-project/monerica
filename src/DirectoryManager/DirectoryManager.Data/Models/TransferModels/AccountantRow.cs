namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class AccountantRow
    {
        public decimal Quantity { get; init; }
        public string Description { get; init; } = "";
        public DateTime PaidDateUtc { get; init; }
        public decimal SalesPrice { get; init; }
        public decimal Cost { get; init; }
    }
}
