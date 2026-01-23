using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class DirectoryFilterViewModel
    {
        public DirectoryFilterQuery Query { get; set; } = new ();

        public int TotalCount { get; set; }
        public List<DirectoryEntryViewModel> Entries { get; set; } = new ();

        // Options
        public List<(string Code, string Name)> CountryOptions { get; set; } = new ();
        public List<IdNameOption> CategoryOptions { get; set; } = new ();
        public List<IdNameOption> SubCategoryOptions { get; set; } = new ();

        // For status checkbox list
        public List<DirectoryManager.Data.Enums.DirectoryStatus> AllStatuses { get; set; } = new ();

        public CategorySponsorModel? CategorySponsorModel { get; set; }
        public SubcategorySponsorModel? SubcategorySponsorModel { get; set; }
    }
}
