using System.ComponentModel.DataAnnotations;
using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Web.Models.ProblemReports
{
    public class CreateProblemReportInputModel : IValidatableObject
    {
        [Required(ErrorMessage = "Please describe the problem with this listing.")]
        [StringLength(4000, MinimumLength = 10, ErrorMessage = "Please enter at least 10 characters.")]
        [Display(Name = "What's the problem?")]
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
