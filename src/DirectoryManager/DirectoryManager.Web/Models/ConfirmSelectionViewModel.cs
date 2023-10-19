
namespace DirectoryManager.Web.Models
{
    public class ConfirmSelectionViewModel
    {
        public DirectoryEntryViewModel SelectedDirectoryEntry { get; set; }
        public SponsoredListingOffer Offer { get; set; }
        public bool CanCreateSponsoredListing { get; set; } = true;
        public DateTime NextListingExpiration { get; set; }
    }
}