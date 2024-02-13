using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class EmailSubscribeModel
    {
        [EmailAddress]
        [Required]
        public string Email { get; set; } = default!;
    }
}