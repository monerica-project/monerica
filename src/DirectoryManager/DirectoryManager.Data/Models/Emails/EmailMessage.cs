using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    public class EmailMessage : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailMessageId { get; set; }

        [StringLength(100)]
        [Required]
        public string EmailKey { get; set; } = string.Empty;

        [StringLength(255)]
        [Required]
        public string EmailSubject { get; set; } = string.Empty;

        public string EmailBodyText { get; set; } = string.Empty;

        public string EmailBodyHtml { get; set; } = string.Empty;
    }
}