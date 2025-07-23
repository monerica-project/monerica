using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Emails
{
    public class EmailSubscribeModel
    {
        [EmailAddress]
        [Required]
        public string Email { get; set; } = default!;

        public string Captcha { get; set; }
    }
}