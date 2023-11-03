using DirectoryManager.Data.Models;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Helpers
{
    public static class ViewModelConverter
    {
        public static List<DirectoryEntryViewModel> ConvertToViewModels(
                List<DirectoryEntry> directoryEntries,
                DateDisplayOption option = DateDisplayOption.NotDisplayed,
                string link2Name = "",
                string link3Name = "")
        {
            return directoryEntries.Select(entry => new DirectoryEntryViewModel
            {
                DirectoryEntry = entry,
                DateOption = option,
                Link2Name = link2Name,
                Link3Name = link3Name,
            }).ToList();
        }
    }
}