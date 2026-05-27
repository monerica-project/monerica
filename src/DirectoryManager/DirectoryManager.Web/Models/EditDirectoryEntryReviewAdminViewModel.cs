using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class EditDirectoryEntryReviewAdminViewModel
    {
        public int DirectoryEntryReviewId { get; set; }

        [Required]
        [Display(Name = "Directory Entry Id")]
        public int DirectoryEntryId { get; set; }

        [Range(1, 5)]
        public byte? Rating { get; set; }

        [Required]
        [StringLength(8000, MinimumLength = 1)]
        public string Body { get; set; } = string.Empty;

        [StringLength(2048)]
        [Display(Name = "Order Proof (optional)")]
        public string? OrderProof { get; set; }

        [Display(Name = "Moderation Status")]
        public ReviewModerationStatus ModerationStatus { get; set; }

        [StringLength(2000)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        public List<int> SelectedTagIds { get; set; } = new ();

        public List<TagOption> AllTags { get; set; } = new ();

        public class TagOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
        }
    }
}