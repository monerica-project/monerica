using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Models
{
    public class ConfirmSelectionViewModel
    {
        public DirectoryEntryViewModel SelectedDirectoryEntry { get; set; }
        public SponsoredListingOffer Offer { get; set; }
    }
}
