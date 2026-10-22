using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    public class ReservationGroupHelper
    {
        public static string BuildReservationGroupName(SponsorshipType sponsorshipType, int? typeId = null)
        {
            typeId ??= 0;

            if (sponsorshipType == SponsorshipType.MainSponsor)
            {
                typeId = 0;
            }

            return string.Format("{0}-{1}", sponsorshipType.ToString(), typeId);
        }

        /// <summary>
        /// Group for the per-subcategory "shadow" hold a Main sponsor checkout places, so only one
        /// Main checkout runs per subcategory at a time. Kept separate from the global Main group
        /// ("MainSponsor-0"), otherwise every Main checkout would count twice there and block Main
        /// checkouts in every other subcategory.
        /// </summary>
        public static string BuildMainSponsorSubcategoryGroupName(int subCategoryId) =>
            $"MainSponsorSubcategory-{subCategoryId}";
    }
}