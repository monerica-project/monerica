namespace DirectoryManager.Web.Models.Reports
{
    /// <summary>
    /// Simple row to show an advertiser involved in activation/churn.
    /// </summary>
    public sealed class ChurnActorRow
    {
        public int DirectoryEntryId { get; set; }

        public string DirectoryEntryName { get; set; } = string.Empty;
    }
}
