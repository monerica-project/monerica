using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingReservation : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SponsoredListingReservationId { get; set; }

        public Guid ReservationGuid { get; set; }

        public DateTime ExpirationDateTime { get; set; }

        /// <summary>
        /// A group used to distinguish different types of checkout reservations such as main or sub-catetgory.
        /// </summary>
        [MaxLength(100)]
        required public string ReservationGroup { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable context for the reservation at creation time:
        /// sponsorship type, scope (category/subcategory), and listing info.
        /// </summary>
        [MaxLength(1000)]
        public string? Details { get; set; }
    }
}