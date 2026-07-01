namespace DirectoryManager.Web.Models.ListingHistory
{
    public sealed class ListingHistoryRevision
    {
        public DateTime TimestampUtc { get; set; }

        /// <summary>True for the first snapshot (the listing being added).</summary>
        public bool IsCreation { get; set; }

        public IReadOnlyList<ListingFieldChange> Changes { get; set; }
            = new List<ListingFieldChange>();
    }
}
