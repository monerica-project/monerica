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
