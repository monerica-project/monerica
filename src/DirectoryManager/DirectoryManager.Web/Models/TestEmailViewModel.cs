using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class TestEmailViewModel
    {
        [Required]
        [EmailAddress]
        public string RecipientEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(255, ErrorMessage = "Subject must be less than 255 characters.")]
        public string Subject { get; set; } = "Test Email";

        [Required]
        public string BodyText { get; set; } = "This is a test email sent from the application.";

        [Required]
        public string BodyHtml { get; set; } = "<p>This is a <strong>test email</strong> sent from the application.</p>";
    }
}