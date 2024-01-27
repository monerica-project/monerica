using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingReservation : StateInfo
    {
        public int SponsoredListingReservationId { get; set; }

        public Guid ReservationId { get; set; }

        public DateTime ExpirationDateTime { get; set; }
    }
}