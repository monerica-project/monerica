namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class ApprovedReviewStatsRow
    {
        public int DirectoryEntryId { get; set; }
        public int Count { get; set; }

        // Approved crowd reviews only (excludes pinned official reviews) — the count the listing
        // profile pages by, so it is what review page URLs must be computed from.
        public int PagedCount { get; set; }
        public DateTime Last { get; set; }
    }
}