namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>
    /// Which categories / subcategories have at least one listing that could actually buy a
    /// sponsorship there right now. "Advertise here" is only shown on those pages.
    /// </summary>
    public interface ISponsorableScopeService
    {
        Task<bool> CategoryHasBuyerAsync(int categoryId);

        Task<bool> SubcategoryHasBuyerAsync(int subCategoryId);
    }
}
