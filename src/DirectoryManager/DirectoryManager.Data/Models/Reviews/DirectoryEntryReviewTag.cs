using System.ComponentModel.DataAnnotations.Schema;

namespace DirectoryManager.Data.Models.Reviews
{
    public class DirectoryEntryReviewTag
    {
        public int DirectoryEntryReviewId { get; set; }
        public DirectoryEntryReview DirectoryEntryReview { get; set; } = null!;

        public int ReviewTagId { get; set; }
        public ReviewTag ReviewTag { get; set; } = null!;

        // Optional: who/when tagged (nice for audit)
        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        public string? CreatedByUserId { get; set; }
    }
}
