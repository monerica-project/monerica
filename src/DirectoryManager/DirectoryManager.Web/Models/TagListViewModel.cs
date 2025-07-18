using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Models
{
    public class TagListViewModel
    {
        public PagedResult<TagCount> PagedTags { get; set; } = new ();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
