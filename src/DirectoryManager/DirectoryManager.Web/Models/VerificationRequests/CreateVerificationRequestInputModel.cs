using System.ComponentModel.DataAnnotations;
using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Web.Models.VerificationRequests
{
    public class CreateVerificationRequestInputModel : IValidatableObject
    {
        [Required(ErrorMessage = "Please tell us why you'd like this listing verified.")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "Please enter at least 10 characters.")]
        [Display(Name = "Why should this be verified?")]
        public string Comment { get; set; } = string.Empty;

        // Honeypot. Real users never fill this in.
        public string? Website { get; set; }

        // Comments are plain text: links are fine, HTML and JavaScript are not.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (InputHtmlGuard.ContainsMarkupAllowingUrls(this.Comment))
            {
                yield return InputHtmlGuard.CommentViolation(nameof(this.Comment));
            }
        }
    }
}
