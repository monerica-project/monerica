using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    /// <summary>The newest review received (any status), for the account home "Last review" cell.</summary>
    public class LastReviewInfo
    {
        public int ReviewId { get; set; }

        public string ListingName { get; set; } = string.Empty;

        public string ListingKey { get; set; } = string.Empty;

        public byte? Rating { get; set; }

        public DateTime CreatedUtc { get; set; }

        public ReviewModerationStatus Status { get; set; }

        public AutoModerationResult AutoResult { get; set; }

        /// <summary>Short status: "auto-approved", "approved", "pending", "flagged", "rejected".</summary>
        public string StatusLabel => this.Status switch
        {
            ReviewModerationStatus.Approved when this.AutoResult == AutoModerationResult.AutoApproved => "auto-approved",
            ReviewModerationStatus.Approved => "approved",
            ReviewModerationStatus.Pending => "pending",
            ReviewModerationStatus.Flagged => "flagged",
            ReviewModerationStatus.Rejected => "rejected",
            _ => this.Status.ToString().ToLowerInvariant(),
        };
    }
}
