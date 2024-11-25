using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Emails
{
    public class EmailMessageModel
    {
        public int EmailMessageId { get; set; }

        [Required]
        [StringLength(100)]
        public string EmailKey { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string EmailSubject { get; set; } = string.Empty;

        public string EmailBodyText { get; set; } = string.Empty;

        public string EmailBodyHtml { get; set; } = string.Empty;
    }
}
