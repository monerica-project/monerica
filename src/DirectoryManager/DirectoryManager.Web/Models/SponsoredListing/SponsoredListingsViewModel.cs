using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models.SponsoredListing
{
    public class SponsoredListingsViewModel
    {
        required public List<DirectoryEntryViewModel> CurrentListings { get; set; }
    }
}