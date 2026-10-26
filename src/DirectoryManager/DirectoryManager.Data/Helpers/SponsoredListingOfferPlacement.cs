using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Rules for which placement a sponsorship price list belongs to.
    ///  - Main sponsor: one standard price list (no category, no subcategory).
    ///  - Category sponsor: the standard price list, or a price list for ONE category page.
    ///  - Subcategory sponsor: the standard price list, or a price list for ONE subcategory page.
    /// A placement without its own prices uses the standard price list for its type.
    /// </summary>
    public static class SponsoredListingOfferPlacement
    {
        public const string StandardLabel = "Standard price";

        /// <summary>
        /// Clears fields that don't apply to the offer's type and returns an error message when
        /// the combination is invalid (null when the offer is fine to save).
        /// </summary>
        public static string? Normalize(SponsoredListingOffer offer)
        {
            ArgumentNullException.ThrowIfNull(offer);

            switch (offer.SponsorshipType)
            {
                case SponsorshipType.MainSponsor:
                    if (offer.CategoryId.HasValue || offer.SubcategoryId.HasValue)
                    {
                        return "Main sponsor prices apply sitewide; leave category and subcategory empty.";
                    }

                    return null;

                case SponsorshipType.CategorySponsor:
                    if (offer.SubcategoryId.HasValue)
                    {
                        return "Category sponsor prices are set per category page; pick a category, not a subcategory.";
                    }

                    return null;

                case SponsorshipType.SubcategorySponsor:
                    if (offer.CategoryId.HasValue)
                    {
                        return "Subcategory sponsor prices are set per subcategory page; pick a subcategory, not a category.";
                    }

                    return null;

                default:
                    return "Pick a sponsorship type.";
            }
        }

        /// <summary>True when the offer is the standard (default) price for its type.</summary>
        public static bool IsStandard(SponsoredListingOffer offer) =>
            offer.CategoryId == null && offer.SubcategoryId == null;

        /// <summary>The placement id the offer is priced for (null = standard price).</summary>
        public static int? PlacementId(SponsoredListingOffer offer) =>
            offer.SponsorshipType switch
            {
                SponsorshipType.CategorySponsor => offer.CategoryId,
                SponsorshipType.SubcategorySponsor => offer.SubcategoryId,
                _ => null,
            };

        /// <summary>"Standard price", "Exchanges" or "Exchanges > Instant Swaps".</summary>
        public static string Label(SponsoredListingOffer offer)
        {
            if (offer.Subcategory != null)
            {
                var catName = offer.Subcategory.Category?.Name;
                return string.IsNullOrWhiteSpace(catName)
                    ? offer.Subcategory.Name
                    : $"{catName} > {offer.Subcategory.Name}";
            }

            if (offer.Category != null)
            {
                return offer.Category.Name;
            }

            return StandardLabel;
        }

        /// <summary>
        /// True when the offer's placement can still be bought: the standard price, an enabled
        /// category page, or an enabled subcategory page (in an enabled category) with sponsorship on.
        /// Needs Category / Subcategory (with its Category) loaded.
        /// </summary>
        public static bool IsLivePlacement(SponsoredListingOffer offer)
        {
            if (IsStandard(offer))
            {
                return true;
            }

            if (offer.SubcategoryId.HasValue)
            {
                var sub = offer.Subcategory;
                return sub != null && sub.IsEnabled && !sub.SponsorshipDisabled
                    && (sub.Category == null || sub.Category.IsEnabled);
            }

            return offer.Category != null && offer.Category.IsEnabled;
        }

        /// <summary>Sort key: standard price first, then placements by name.</summary>
        public static string SortKey(SponsoredListingOffer offer) =>
            IsStandard(offer) ? "0" : "1 " + Label(offer);
    }
}
