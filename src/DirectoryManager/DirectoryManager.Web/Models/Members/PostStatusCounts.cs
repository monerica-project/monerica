using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Members
{
    /// <summary>Counts of posts by moderation state.</summary>
    public sealed class PostStatusCounts
    {
        public int Published { get; set; }

        /// <summary>Pending or flagged: waiting for a moderator.</summary>
        public int Waiting { get; set; }

        /// <summary>Rejected (including hidden by a block).</summary>
        public int Rejected { get; set; }

        public int Total => this.Published + this.Waiting + this.Rejected;

        public void Add(ReviewModerationStatus status, int count)
        {
            switch (status)
            {
                case ReviewModerationStatus.Approved:
                    this.Published += count;
                    break;
                case ReviewModerationStatus.Pending:
                case ReviewModerationStatus.Flagged:
                    this.Waiting += count;
                    break;
                default:
                    this.Rejected += count;
                    break;
            }
        }
    }
}
