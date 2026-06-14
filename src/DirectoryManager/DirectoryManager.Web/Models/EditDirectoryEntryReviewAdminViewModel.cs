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

        [StringLength(2048)]
        [Display(Name = "Verification Details (optional)")]
        public string? OrderProofContext { get; set; }

        // ----- Official review -----
        [Display(Name = "Official review")]
        public bool IsOfficial { get; set; }

        [Display(Name = "Tested date (UTC)")]
        [DataType(DataType.Date)]
        public DateTime? TestedAt { get; set; }

        [StringLength(2048)]
        [Display(Name = "Screenshot URL (optional)")]
        public string? ImageUrl { get; set; }

        [StringLength(2048)]
        [Display(Name = "Sending transaction URL (optional)")]
        public string? SendingTxUrl { get; set; }

        [StringLength(2048)]
        [Display(Name = "Receiving transaction URL (optional)")]
        public string? ReceivingTxUrl { get; set; }

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