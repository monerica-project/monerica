using System.ComponentModel.DataAnnotations;

public class CreateDirectoryEntryReviewReplyInputModel
{
    public int DirectoryEntryReviewId { get; set; }
    public int? ParentCommentId { get; set; }

    [Required(ErrorMessage = "Please write a reply.")]
    [Display(Name = "Reply")]
    [MinLength(36, ErrorMessage = "Please add a bit more detail so your reply is helpful to others.")]
    [StringLength(20000, ErrorMessage = "Reply is too long.")]
    public string Body { get; set; } = string.Empty;
}