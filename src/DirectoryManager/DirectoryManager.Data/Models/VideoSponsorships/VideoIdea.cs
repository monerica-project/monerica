using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.VideoSponsorships
{
    // An upcoming video in the queue. One sponsor per video: a buyer holds it while their
    // invoice is open (HeldByOrderId/HeldUntilUtc) and owns it once paid (SponsoredByOrderId).
    public class VideoIdea : StateInfo
    {
        [Key]
        public int VideoIdeaId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public VideoIdeaStatus Status { get; set; } = VideoIdeaStatus.Queued;

        public int SortOrder { get; set; }

        // Created automatically from a paid dedicated-video order (not selectable by other buyers).
        public bool IsDedicated { get; set; }

        // Checkout lock: the order currently holding this video, and until when.
        public int? HeldByOrderId { get; set; }

        public DateTime? HeldUntilUtc { get; set; }

        // Set once an order for this video is paid. Never offered to other buyers afterwards.
        public int? SponsoredByOrderId { get; set; }

        // The published video (set when the video is completed).
        [MaxLength(500)]
        public string? VideoUrl { get; set; }
    }
}
