namespace DirectoryManager.Web.Models.ListingHistory
{
    /// <summary>
    /// Public, read-only change history for a single directory listing.
    /// Built by diffing consecutive audit snapshots so only fields that
    /// actually changed are shown.
    /// </summary>
    public sealed class ListingHistoryViewModel
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Absolute URL back to the public listing page.</summary>
        public string? ListingUrl { get; set; }

        /// <summary>Newest-first list of revisions that contained real changes.</summary>
        public IReadOnlyList<ListingHistoryRevision> Revisions { get; set; }
            = new List<ListingHistoryRevision>();

        public bool HasHistory => this.Revisions.Count > 0;
    }
}
