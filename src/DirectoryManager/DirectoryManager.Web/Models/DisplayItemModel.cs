using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class DisplayItemModel
    {
        public required IList<Category> Categetories { get; set; }

        public required IList<SubCategory> SubCategetories { get; set; }
    }
}
