using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class SubscribeViewModel
    {
        // the “business” bits
        public SponsorshipType SponsorshipType { get; set; }
        public int? TypeId { get; set; }
        public string Email { get; set; }

        // friendly labels
        public string? CategoryOrSubcategoryName { get; set; }

        // Ui feedback
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
    }
}
