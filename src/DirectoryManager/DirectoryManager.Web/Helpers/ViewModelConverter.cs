﻿using DirectoryManager.Data.Models;
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
                DateOption = Enums.DateDisplayOption.NotDisplayed,
                IsSponsored = false,
                Link2Name = link2Name,
                Link3Name = link3Name,
                Link = entry.Link,
                Name = entry.Name,
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
            }).ToList();
        }
    }
}