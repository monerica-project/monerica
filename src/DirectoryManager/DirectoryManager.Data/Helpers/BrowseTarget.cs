namespace DirectoryManager.Data.Helpers
{
    /// <summary>A page a search can link to: a category, subcategory, tag, country or site page.</summary>
    /// <param name="Label">Link text.</param>
    /// <param name="Path">Site-relative path ("/vpns", "/phone-and-sms/esims-and-sim-cards").</param>
    /// <param name="Kind">What kind of page it is.</param>
    /// <param name="Name">The name matched against the search (a country's ISO code for countries).</param>
    public sealed record BrowseTarget(string Label, string Path, SearchBrowseLinks.TargetKind Kind, string Name);
}
