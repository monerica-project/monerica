using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryAdditionalSubcategoryRepository
    {
        /// <summary>A listing's additional subcategories (with SubCategory and Category loaded).</summary>
        Task<List<DirectoryEntryAdditionalSubcategory>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default);

        /// <summary>Every (listing, additional subcategory) pair, no navigation properties loaded.</summary>
        Task<List<(int DirectoryEntryId, int SubCategoryId)>> GetAllPairsAsync(CancellationToken ct = default);

        /// <summary>
        /// Replaces a listing's additional subcategories with <paramref name="rows"/>: rows for the
        /// listing's main subcategory, duplicates and anything over the limit are ignored.
        /// Existing rows for the same subcategory are updated in place (keeping their id).
        /// </summary>
        /// <summary>Deletes the listing's additional-subcategory row for this subcategory, if any (e.g. when it became the main one).</summary>
        Task RemoveAsync(int directoryEntryId, int subCategoryId, CancellationToken ct = default);

        Task SyncAsync(int directoryEntryId, int mainSubCategoryId, IEnumerable<DirectoryEntryAdditionalSubcategory> rows, CancellationToken ct = default);
    }
}
