namespace DirectoryManager.Web.Models.StaticPublishing
{
    /// <summary>A paged list page (a tag or a country): its slug and how many listings it holds.</summary>
    public class PagedPageFacts
    {
        public string Slug { get; set; } = string.Empty;

        public int EntryCount { get; set; }
    }
}
