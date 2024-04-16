using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    public class ReservationGroupHelper
    {
        public static string CreateReservationGroup(SponsorshipType sponsorshipType, int? subCategoryId = null)
        {
            subCategoryId ??= 0;

            return string.Format("{0}-{1}", sponsorshipType.ToString(), subCategoryId);
        }
    }
}