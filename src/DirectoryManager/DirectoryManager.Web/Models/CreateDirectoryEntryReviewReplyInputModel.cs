using System.ComponentModel.DataAnnotations;
using DirectoryManager.Web.ModelBinding;

public class CreateDirectoryEntryReviewReplyInputModel
{
    public int DirectoryEntryReviewId { get; set; }
    public int? ParentCommentId { get; set; }

    [Required(ErrorMessage = "Please write a reply.")]
    [Display(Name = "Reply")]
    [StringLength(4000, ErrorMessage = "Your reply is too long. Please keep it under {1} characters.")]
    [CleanMultiLine]
    // NOTE: the minimum-length rule lives solely in UserContentModerationService
    // (MinLengthReplyChars) so the "add a bit more detail" message is raised exactly once.
    public string Body { get; set; } = string.Empty;

    // ---- Only used when the replier is the review's own author (checked server-side) ----

    /// <summary>True when the form included the "update your review" fields (author only).</summary>
    public bool EditOwnReview { get; set; }

    /// <summary>The author's new star rating (1–5) for their review.</summary>
    [Range(1, 5)]
    public byte? NewRating { get; set; }

    /// <summary>Public tags the author wants on their review (checkboxes).</summary>
    public List<int>? SelectedReviewTagIds { get; set; }

    /// <summary>The single "order amount" band the author picked, if any.</summary>
    public int? SelectedMoneyBandTagId { get; set; }
}
