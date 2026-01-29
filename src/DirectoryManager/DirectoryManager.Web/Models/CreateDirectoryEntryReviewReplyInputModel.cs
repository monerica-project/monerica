using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class CreateDirectoryEntryReviewReplyInputModel
    {
        [Required]
        public int DirectoryEntryReviewId { get; set; }

        public int? ParentCommentId { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;
    }
}