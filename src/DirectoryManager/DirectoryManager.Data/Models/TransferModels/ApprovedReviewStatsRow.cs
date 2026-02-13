namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class ApprovedReviewStatsRow
    {
        public int DirectoryEntryId { get; set; }
        public int Count { get; set; }
        public DateTime Last { get; set; }
    }
}