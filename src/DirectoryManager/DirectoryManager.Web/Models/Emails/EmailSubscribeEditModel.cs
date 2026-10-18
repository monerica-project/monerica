using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Emails
{
    public class EmailSubscribeEditModel
    {
        [EmailAddress]
        [Required]
        public string Email { get; set; } = default!;

        public int EmailSubscriptionId { get; set; }

        public DateTime CreateDate { get; set; }

        public bool IsSubscribed { get; set; }
    }
}
