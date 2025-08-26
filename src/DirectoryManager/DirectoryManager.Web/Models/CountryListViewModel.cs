using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{

    public class CountryListViewModel
    {
        public PagedResult<CountryWithCount> PagedCountries { get; set; } = new ();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
