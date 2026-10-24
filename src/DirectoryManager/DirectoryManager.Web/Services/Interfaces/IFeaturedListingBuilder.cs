using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>
    /// Builds the display model for the admin-picked unpaid "Featured" listing shown in an
    /// unsold category / subcategory sponsor spot.
    /// </summary>
    public interface IFeaturedListingBuilder
    {
        /// <summary>
        /// Returns the featured listing ready to render, or null when nothing is set or the listing
        /// is no longer Admitted/Verified. Pass the subcategory id on subcategory pages so a listing
        /// featured through one of its additional subcategories shows that subcategory's wording.
        /// </summary>
        Task<DirectoryEntryViewModel?> BuildAsync(int? directoryEntryId, int? subCategoryId = null);
    }
}
