using System.ComponentModel.DataAnnotations;
using DirectoryManager.Web.ModelBinding;

namespace DirectoryManager.Web.Models
{
    public class CreateDirectoryEntryReviewInputModel
    {
        [Required]
        public int DirectoryEntryId { get; set; }

        [Range(1, 5)]
        public byte? Rating { get; set; }

        [Required]
        [StringLength(10000, ErrorMessage = "Your review is too long. Please keep it under {1} characters.")]
        [CleanMultiLine]
        public string? Body { get; set; }

        // ✅ Single field used by public + admin create views
        // URL/id — validated separately in the controller; leave raw.
        [MaxLength(2048)]
        public string? OrderProof { get; set; }

        // Optional supporting context (e.g. receiving wallet address) a moderator can use
        // to look up the order. Surfaced when the entry's subcategory requires review
        // verification; the field itself is always optional.
        [MaxLength(2048)]
        public string? OrderProofContext { get; set; }

        // Optional swap facts (exchange listings only): minutes the swap took and the number of
        // confirmations the exchange waited for. Shown with the review when given.
        [Range(1, 43200, ErrorMessage = "Processing time must be between 1 and 43,200 minutes (30 days), or left blank.")]
        [Display(Name = "Processing time (minutes)")]
        public int? ProcessingMinutes { get; set; }

        [Range(0, 1000, ErrorMessage = "Confirmations must be between 0 and 1,000, or left blank.")]
        [Display(Name = "Confirmations")]
        public int? Confirmations { get; set; }

        // Review tags the reviewer suggests (checkbox selection). Only enabled tags are honored;
        // the admin reviews and can approve or modify these during moderation.
        public List<int>? SelectedReviewTagIds { get; set; }

        // The single money-band tag chosen from the "Order amount" dropdown (bound separately from
        // the checkbox list so an empty "prefer not to say" selection binds cleanly to null instead
        // of pushing an unparseable "" into SelectedReviewTagIds). Merged into the tag selection in
        // the controller. Only one amount can ever be selected.
        public int? SelectedMoneyBandTagId { get; set; }
    }
}
