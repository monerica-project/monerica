using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models.StaticPublishing
{
    /// <summary>One publish attempt result (instant, manual, or from a full crawl). Kept 30 days.</summary>
    public class StaticPublishLogEntry
    {
        public long StaticPublishLogEntryId { get; set; }

        /// <summary>Root-relative URL path, or "*" for a full-crawl summary row.</summary>
        [Required]
        [MaxLength(StaticPublishConstants.MaxPathLength)]
        public string Path { get; set; } = string.Empty;

        [MaxLength(StaticPublishConstants.MaxReasonLength)]
        public string? Reason { get; set; }

        /// <summary>When the change was first queued (null for full-crawl rows).</summary>
        public DateTime? RequestedUtc { get; set; }

        /// <summary>When the result came back.</summary>
        public DateTime PublishedUtc { get; set; }

        public int DurationMs { get; set; }

        /// <summary>See <see cref="StaticPublishConstants"/> Result* values.</summary>
        [Required]
        [MaxLength(20)]
        public string Result { get; set; } = StaticPublishConstants.ResultFailed;

        [MaxLength(StaticPublishConstants.MaxErrorLength)]
        public string? Error { get; set; }

        [Required]
        [MaxLength(20)]
        public string Source { get; set; } = StaticPublishConstants.SourceInstant;
    }
}
