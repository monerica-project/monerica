namespace DirectoryManager.Web.Models
{
    public class BreadcrumbItem
    {
        public int Position { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
