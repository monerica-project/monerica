namespace DirectoryManager.Web.Models.ListingHistory
{
    public sealed class ListingFieldChange
    {
        public string Field { get; set; } = string.Empty;

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        /// <summary>When true, render only the value (initial state), no "old → new".</summary>
        public bool IsInitial { get; set; }
    }
}
