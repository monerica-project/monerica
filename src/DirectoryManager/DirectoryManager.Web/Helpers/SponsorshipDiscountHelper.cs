using System;
using System.Threading.Tasks;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Cross-tier existing-sponsor perk: a listing that already holds an active sponsorship of one tier
    /// gets a percent off placements of a DIFFERENT tier (never a same-tier renewal). Highest held
    /// tier wins (Main 20% &gt; Category 15% &gt; Subcategory 10%) — checked in that order so the first
    /// active match is the highest. This is the single source of truth used by both the checkout flow
    /// and the sponsor options page, so the displayed and charged prices never diverge. Computed
    /// server-side; a buyer can never influence it.
    /// </summary>
    public static class SponsorshipDiscountHelper
    {
        public static async Task<(decimal Percent, SponsorshipType? BasisTier)> GetCrossTierDiscountAsync(
            ISponsoredListingRepository repo, int directoryEntryId, SponsorshipType targetType)
        {
            var holdsMain = await repo.IsSponsoredListingActive(directoryEntryId, SponsorshipType.MainSponsor).ConfigureAwait(false);
            var holdsCategory = await repo.IsSponsoredListingActive(directoryEntryId, SponsorshipType.CategorySponsor).ConfigureAwait(false);
            var holdsSubcategory = await repo.IsSponsoredListingActive(directoryEntryId, SponsorshipType.SubcategorySponsor).ConfigureAwait(false);
            return ResolveCrossTierPerk(targetType, holdsMain, holdsCategory, holdsSubcategory);
        }

        /// <summary>
        /// Pure matrix: given the tiers a listing currently holds and the tier it's buying, returns the
        /// existing-sponsor perk percent and the tier that granted it. Buying a tier they already hold is
        /// a renewal (no perk); otherwise the highest held tier wins (Main 20% &gt; Category 15% &gt; Sub 10%).
        /// </summary>
        internal static (decimal Percent, SponsorshipType? BasisTier) ResolveCrossTierPerk(
            SponsorshipType targetType, bool holdsMain, bool holdsCategory, bool holdsSubcategory)
        {
            var holdsTarget = (targetType == SponsorshipType.MainSponsor && holdsMain)
                || (targetType == SponsorshipType.CategorySponsor && holdsCategory)
                || (targetType == SponsorshipType.SubcategorySponsor && holdsSubcategory);
            if (holdsTarget)
            {
                return (0m, null);
            }

            if (holdsMain && targetType != SponsorshipType.MainSponsor)
            {
                return (DirectoryManager.Common.Constants.IntegerConstants.MainSponsorCrossTierDiscountPercent, SponsorshipType.MainSponsor);
            }

            if (holdsCategory && targetType != SponsorshipType.CategorySponsor)
            {
                return (DirectoryManager.Common.Constants.IntegerConstants.CategorySponsorCrossTierDiscountPercent, SponsorshipType.CategorySponsor);
            }

            if (holdsSubcategory && targetType != SponsorshipType.SubcategorySponsor)
            {
                return (DirectoryManager.Common.Constants.IntegerConstants.SubcategorySponsorCrossTierDiscountPercent, SponsorshipType.SubcategorySponsor);
            }

            return (0m, null);
        }

        public static decimal ApplyPercentDiscount(decimal price, decimal percent)
        {
            if (percent <= 0m)
            {
                return price;
            }

            return decimal.Round(price - ((price * percent) / 100m), 2, MidpointRounding.AwayFromZero);
        }
    }
}
