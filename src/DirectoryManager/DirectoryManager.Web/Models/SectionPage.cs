namespace DirectoryManager.Web.Models
{
    public class SectionPage
    {
        public string CanonicalUrl { get; set; } = default!;

        public string AnchorText { get; set; } = default!;

        public bool HasChildren
        {
            get
            {
                return this.ChildPages.Count > 0;
            }
        }

        public List<SectionPage> ChildPages { get; set; } = new List<SectionPage>();
    }
}