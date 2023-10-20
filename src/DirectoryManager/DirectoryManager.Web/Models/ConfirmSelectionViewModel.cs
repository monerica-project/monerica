namespace DirectoryManager.Web.Models
{
    public class ConfirmSelectionViewModel
    {
        required public DirectoryEntryViewModel SelectedDirectoryEntry { get; set; }
        required public SponsoredListingOffer Offer { get; set; }
        public bool CanCreateSponsoredListing { get; set; } = true;
        public DateTime NextListingExpiration { get; set; }
    }
}