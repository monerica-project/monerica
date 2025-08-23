using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class CreateDirectoryEntryReviewInputModel
    {
        [Required]
        [Display(Name = "Directory Entry Id")]
        public int DirectoryEntryId { get; set; }

        // Make rating optional; if you want a range, uncomment the Range attribute and set your scale
        [Range(1, 10, ErrorMessage = "Rating must be between {1} and {2}.")]
        public byte? Rating { get; set; }

        [Required]
        [Display(Name = "Review")]
        [StringLength(20000, ErrorMessage = "Review is too long.")]
        public string Body { get; set; } = string.Empty;
    }
}
