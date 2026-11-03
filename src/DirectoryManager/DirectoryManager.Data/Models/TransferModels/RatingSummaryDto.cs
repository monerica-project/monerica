namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class RatingSummaryDto
    {
        public int DirectoryEntryId { get; set; }
        public double AvgRating { get; set; }
        public int ReviewCount { get; set; }

        /// <summary>Approved reviews tagged "Valid Order": the order link was checked by hand.</summary>
        public int VerifiedOrderCount { get; set; }
    }
}