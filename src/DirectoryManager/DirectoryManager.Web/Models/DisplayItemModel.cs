using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class DisplayItemModel
    {

        public IList<Category> Categetories { get; set; }

        public IList<SubCategory> SubCategetories { get; set; }
    }
}
