namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class RatingSummaryDto
    {
        public int DirectoryEntryId { get; set; }
        public double AvgRating { get; set; }
        public int ReviewCount { get; set; }
    }
}