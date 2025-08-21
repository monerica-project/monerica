using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public sealed class StartReservationInput
    {
        public int DirectoryEntryId { get; set; }
        public SponsorshipType SponsorshipType { get; set; }
        public Guid? RsvId { get; set; }
        public string? Website { get; set; } // must be empty
        public string? Js { get; set; } // must be "1"
    }
}