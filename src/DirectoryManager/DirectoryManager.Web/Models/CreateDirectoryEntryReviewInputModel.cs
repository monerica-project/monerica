using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class CreateDirectoryEntryReviewInputModel
    {
        [Required]
        public int DirectoryEntryId { get; set; }

        [Range(1, 5)]
        public byte? Rating { get; set; }

        [Required]
        public string? Body { get; set; }

        // ✅ Single field used by public + admin create views
        [MaxLength(2048)]
        public string? OrderProof { get; set; }
    }
}
