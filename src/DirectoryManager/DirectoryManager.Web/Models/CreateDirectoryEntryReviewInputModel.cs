using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class CreateDirectoryEntryReviewInputModel
    {
        [Required]
        [Display(Name = "Directory Entry Id")]
        public int DirectoryEntryId { get; set; }

        [Range(1, 10, ErrorMessage = "Rating must be between {1} and {2}.")]
        [Required(ErrorMessage = "Please select a rating.")]
        public byte? Rating { get; set; }

        [Required(ErrorMessage = "Please write a review.")]
        [Display(Name = "Review")]
        [MinLength(36, ErrorMessage = "Please add a bit more detail so your review is helpful to others.")]
        [StringLength(20000, ErrorMessage = "Review is too long.")]
        public string Body { get; set; } = string.Empty;
    }
}