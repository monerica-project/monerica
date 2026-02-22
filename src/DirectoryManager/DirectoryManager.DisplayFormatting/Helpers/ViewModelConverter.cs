using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.DisplayFormatting.Helpers
{
    public static class ViewModelConverter
    {
        public static List<DirectoryEntryViewModel> ConvertToViewModels(
                List<DirectoryEntry> directoryEntries,
                DateDisplayOption option = DateDisplayOption.NotDisplayed,
                ItemDisplayType itemDisplayType = ItemDisplayType.Normal,
                string link2Name = "",
                string link3Name = "")
        {
            return directoryEntries.Select(entry => new DirectoryEntryViewModel
            {
                ItemPath = FormattingHelper.ListingPath(entry.DirectoryEntryKey),
                DateOption = option,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = entry.Link,
                Name = entry.Name,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                Contact = entry.Contact,
                Description = entry.Description,
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryStatus = entry.DirectoryStatus,
                Link2 = entry.Link2,
                Link3 = entry.Link3,
                Location = entry.Location,
                Note = entry.Note,
                Processor = entry.Processor,
                SubCategoryId = entry.SubCategoryId,
                SubCategory = entry.SubCategory,
                CreateDate = entry.CreateDate,
                UpdateDate = entry.UpdateDate,
                LinkA = entry.LinkA,
                Link2A = entry.Link2A,
                Link3A = entry.Link3A,
                DirectoryBadge = entry.DirectoryBadge,
                ItemDisplayType = itemDisplayType,
                CountryCode = entry.CountryCode,
                PgpKey = entry.PgpKey,
                FoundedDate = entry.FoundedDate
            }).ToList();
        }
    }
}