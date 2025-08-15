using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public sealed class StartReservationInput
    {
        public int DirectoryEntryId { get; set; }
        public SponsorshipType SponsorshipType { get; set; }
        // optional UX: allow passing a known rsvId to keep an existing one alive
        public Guid? RsvId { get; set; }
        // honeypot + JS proof
        public string? Website { get; set; } // must be empty
        public string? Js { get; set; }      // must be "1"
    }

}
