using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Rules for the unpaid "Featured" listing an admin can pick per category / subcategory.
    /// </summary>
    public static class FeaturedListingRules
    {
        /// <summary>Only Admitted or Verified listings can be featured (no age limit).</summary>
        public static bool IsFeaturable(DirectoryStatus status) =>
            status == DirectoryStatus.Admitted || status == DirectoryStatus.Verified;
    }
}
