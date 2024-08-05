using DirectoryManager.Data.Models;
using DirectoryManager.Web.Enums;

namespace DirectoryManager.Web.Models
{
    public class DirectoryEntryViewModel
    {
        required public DirectoryEntry DirectoryEntry { get; set; }
        public DateDisplayOption DateOption { get; set; } = DateDisplayOption.NotDisplayed;
        public bool IsSponsored { get; set; } = false;
        public bool IsSubCategorySponsor { get; set; } = false;
        public bool DisplayAsSponsoredItem { get; set; } = false;
        public string Link2Name { get; set; } = "Link 2";
        public string Link3Name { get; set; } = "Link 3";
    }
}
