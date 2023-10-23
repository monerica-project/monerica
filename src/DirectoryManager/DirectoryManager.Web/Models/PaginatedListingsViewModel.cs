namespace DirectoryManager.Web.Models
{
    public class PaginatedListingsViewModel
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        required public List<ListingViewModel> Listings { get; set; }
    }
}
