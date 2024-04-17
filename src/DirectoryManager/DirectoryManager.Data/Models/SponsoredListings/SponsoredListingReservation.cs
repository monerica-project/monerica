using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingReservation : StateInfo
    {
        public int SponsoredListingReservationId { get; set; }

        public Guid ReservationGuid { get; set; }

        public DateTime ExpirationDateTime { get; set; }

        /// <summary>
        /// A group used to distinguish different types of checkout reservations such as main or sub-catetgory.
        /// </summary>
        [MaxLength(100)]
        required public string ReservationGroup { get; set; } = string.Empty;
    }
}