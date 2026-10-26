using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models.VideoSponsorships
{
    // One requested video on an order. Each has its own published URL and its own completion.
    public class VideoSponsorOrderItem
    {
        [Key]
        public int VideoSponsorOrderItemId { get; set; }

        public int VideoSponsorOrderId { get; set; }

        public VideoSponsorOrder VideoSponsorOrder { get; set; } = null!;

        // Null for a dedicated video until the order is paid (its queue entry is created then).
        public int? VideoIdeaId { get; set; }

        public VideoIdea? VideoIdea { get; set; }

        // Snapshot of the video title at purchase.
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        // This video's share of the charged amount (USD, after discount).
        public decimal Price { get; set; }

        [MaxLength(500)]
        public string? VideoUrl { get; set; }

        // The blog article written about this video (part of what the sponsor gets).
        [MaxLength(500)]
        public string? BlogArticleUrl { get; set; }

        public DateTime? CompletedUtc { get; set; }

        // The one-time "your video is live" email.
        public DateTime? CompletionEmailSentUtc { get; set; }

        [MaxLength(500)]
        public string? CompletionEmailResult { get; set; }
    }
}
