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
        [StringLength(10000, MinimumLength = 1)] // same limit as the public review form
        public string Body { get; set; } = string.Empty;

        [StringLength(2048)]
        [Display(Name = "Order Proof (optional)")]
        public string? OrderProof { get; set; }

        [StringLength(2048)]
        [Display(Name = "Verification Details (optional)")]
        public string? OrderProofContext { get; set; }

        // Optional swap facts (exchange listings only): minutes the swap took and the number of
        // confirmations the exchange waited for. Shown with the review when given.
        [Range(1, 43200, ErrorMessage = "Processing time must be between 1 and 43,200 minutes (30 days), or left blank.")]
        [Display(Name = "Processing time (minutes)")]
        public int? ProcessingMinutes { get; set; }

        [Range(0, 1000, ErrorMessage = "Confirmations must be between 0 and 1,000, or left blank.")]
        [Display(Name = "Confirmations")]
        public int? Confirmations { get; set; }

        // ----- Official review -----
        [Display(Name = "Official review")]
        public bool IsOfficial { get; set; }

        // When checked, the review is kept but rendered with its entire body struck
        // through (used to show an author retracted it without deleting it).
        [Display(Name = "Strike through (retracted — show body crossed out)")]
        public bool IsStruckThrough { get; set; }

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

        [StringLength(2048)]
        [Display(Name = "AML check screenshot URL (optional)")]
        public string? AmlScreenshotUrl { get; set; }

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