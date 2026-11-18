using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models.StaticPublishing
{
    /// <summary>
    /// A public page that must be (re)published to the static mirror. A row existing means
    /// "needs publishing"; it is deleted once the publisher reports success. One row per path:
    /// enqueuing the same path again keeps the earliest <see cref="RequestedUtc"/> and moves
    /// <see cref="LastRequestedUtc"/>, so a change made while the page is being fetched is never lost.
    /// The special path "*" asks for a full crawl.
    /// </summary>
    public class StaticPublishQueueItem
    {
        public int StaticPublishQueueItemId { get; set; }

        /// <summary>Root-relative URL path, e.g. "/site/offcard".</summary>
        [Required]
        [MaxLength(StaticPublishConstants.MaxPathLength)]
        public string Path { get; set; } = string.Empty;

        /// <summary>When the page first needed publishing (kept on re-enqueue).</summary>
        public DateTime RequestedUtc { get; set; }

        /// <summary>When it was most recently asked for.</summary>
        public DateTime LastRequestedUtc { get; set; }

        [MaxLength(StaticPublishConstants.MaxReasonLength)]
        public string? Reason { get; set; }

        /// <summary><see cref="StaticPublishConstants.SourceInstant"/> or <see cref="StaticPublishConstants.SourceManual"/>.</summary>
        [MaxLength(20)]
        public string Source { get; set; } = StaticPublishConstants.SourceInstant;

        public int Attempts { get; set; }

        [MaxLength(StaticPublishConstants.MaxErrorLength)]
        public string? LastError { get; set; }

        public DateTime? LastAttemptUtc { get; set; }
    }
}
