using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Models
{
    internal class TagManagementIndexViewModel
    {
        public PagedResult<Tag> PagedTags { get; set; } = new ();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
}