using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{

    public class CountryEntriesViewModel
    {
        public string CountryCode { get; set; } = "";
        public string CountryName { get; set; } = "";
        public string CountryKey { get; set; } = ""; // slug
        public PagedResult<DirectoryEntryViewModel> PagedEntries { get; set; } = new ();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
