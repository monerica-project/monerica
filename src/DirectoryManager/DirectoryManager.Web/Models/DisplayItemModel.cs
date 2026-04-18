using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class DisplayItemModel
    {
        required public IList<Category> Categetories { get; set; }

        required public IList<Subcategory> SubCategetories { get; set; }
    }
}
