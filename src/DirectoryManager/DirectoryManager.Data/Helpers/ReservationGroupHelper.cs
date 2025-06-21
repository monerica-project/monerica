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
    }
}