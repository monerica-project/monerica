using DirectoryManager.Web.Enums;

namespace DirectoryManager.Web.Models
{
    public class SiteMapItem
    {
        public string Url { get; set; } = default!;

        public DateTime LastMod { get; set; }

        public ChangeFrequency ChangeFrequency { get; set; }

        public double Priority { get; set; }
    }
}
