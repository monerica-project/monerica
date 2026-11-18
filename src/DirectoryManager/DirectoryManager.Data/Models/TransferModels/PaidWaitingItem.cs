namespace DirectoryManager.Data.Models.TransferModels
{
    // A submission, verification request or problem report whose optional donation was paid
    // and which nobody has handled yet. Shown at the top of the admin home page.
    public sealed class PaidWaitingItem
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTime PaidUtc { get; init; }

        public decimal? Usd { get; init; }

        public decimal? Xmr { get; init; }

        public DateTime CreateDate { get; init; }
    }
}
