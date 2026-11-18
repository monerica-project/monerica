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

        /// <summary>Builds a category page's pick; a listing that is in the category only through an additional subcategory is shown with that subcategory's name and links.</summary>
        /// <returns>The view model, or null when there is no featurable pick.</returns>
        Task<DirectoryEntryViewModel?> BuildForCategoryAsync(int? directoryEntryId, int categoryId);
    }
}
