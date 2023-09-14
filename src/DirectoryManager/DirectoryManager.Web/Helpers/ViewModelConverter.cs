using DirectoryManager.Data.Models;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Helpers
{
    public static class ViewModelConverter
    {
        public static List<DirectoryEntryViewModel> ConvertToViewModels(List<DirectoryEntry> directoryEntries, DateDisplayOption option = DateDisplayOption.NotDisplayed)
        {
            return directoryEntries.Select(entry => new DirectoryEntryViewModel
            {
                DirectoryEntry = entry,
                DateOption = option
            }).ToList();
        }
    }
}
