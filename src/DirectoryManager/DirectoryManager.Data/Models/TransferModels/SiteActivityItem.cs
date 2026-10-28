using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>One thing a person submitted to the site, and when (UTC).</summary>
    public sealed record SiteActivityItem(SiteActivityType Type, DateTime CreateDate);
}
