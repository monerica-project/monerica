using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.VerificationRequests
{
    public class CreateVerificationRequestInputModel
    {
        [Required(ErrorMessage = "Please tell us why you'd like this listing verified.")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "Please enter at least 10 characters.")]
        [Display(Name = "Why should this be verified?")]
        public string Comment { get; set; } = string.Empty;

        // Honeypot. Real users never fill this in.
        public string? Website { get; set; }
    }
}
