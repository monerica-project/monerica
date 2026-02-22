using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class EditDirectoryEntryReviewAdminViewModel
    {
        public int DirectoryEntryReviewId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        [Range(1, 5)]
        public byte? Rating { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Title { get; set; }

        // ✅ Single UI field
        [MaxLength(2048)]
        public string? OrderProof { get; set; }

        public ReviewModerationStatus ModerationStatus { get; set; }
        public string? RejectionReason { get; set; }

        public List<int> SelectedTagIds { get; set; } = new ();
        public List<TagOption> AllTags { get; set; } = new ();

        public class TagOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsEnabled { get; set; }
        }
    }
}
