using System.ComponentModel.DataAnnotations;
using DirectoryManager.Web.ModelBinding;

public class CreateDirectoryEntryReviewReplyInputModel
{
    public int DirectoryEntryReviewId { get; set; }
    public int? ParentCommentId { get; set; }

    [Required(ErrorMessage = "Please write a reply.")]
    [Display(Name = "Reply")]
    [StringLength(20000, ErrorMessage = "Reply is too long.")]
    [CleanMultiLine]
    // NOTE: the minimum-length rule lives solely in UserContentModerationService
    // (MinLengthReplyChars) so the "add a bit more detail" message is raised exactly once.
    public string Body { get; set; } = string.Empty;
}
