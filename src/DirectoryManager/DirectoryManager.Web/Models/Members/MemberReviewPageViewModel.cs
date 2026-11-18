using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Members
{
    public class MemberReviewPageViewModel
    {
        public int DirectoryEntryId { get; set; }

        public string DirectoryEntryKey { get; set; } = string.Empty;

        public string DirectoryEntryName { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public bool RequireOrderProof { get; set; }

        public bool RequireDetails { get; set; }

        /// <summary>Exchange listing: offer the optional processing time and confirmations boxes.</summary>
        public bool ShowSwapFacts { get; set; }

        public bool IsEdit { get; set; }

        public ReviewModerationStatus? CurrentStatus { get; set; }

        public string? RejectionReason { get; set; }

        public MemberReviewInputModel Input { get; set; } = new ();
    }
}
