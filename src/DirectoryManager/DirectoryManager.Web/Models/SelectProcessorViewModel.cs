using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.SponsoredListing
{
    public class SelectProcessorViewModel
    {
        public int DirectoryEntryId { get; set; }
        public int SelectedOfferId { get; set; }
        public Guid? ReservationGuid { get; set; }

        // Display-only fields for the confirmation summary on the page
        public string ListingName { get; set; } = string.Empty;
        public string OfferDescription { get; set; } = string.Empty;
        public decimal OfferPrice { get; set; }
        public int OfferDays { get; set; }

        // The processor the user picks
        public PaymentProcessor SelectedProcessor { get; set; } = PaymentProcessor.NOWPayments;
    }
}
