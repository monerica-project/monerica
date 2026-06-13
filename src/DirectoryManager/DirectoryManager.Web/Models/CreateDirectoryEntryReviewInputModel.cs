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
    }
}
