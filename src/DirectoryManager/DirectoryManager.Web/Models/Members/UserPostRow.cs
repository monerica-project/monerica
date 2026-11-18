using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Members
{
    /// <summary>One review or reply on a user's detail page.</summary>
    public sealed class UserPostRow
    {
        public bool IsReply { get; set; }

        /// <summary>Review id, or reply id when <see cref="IsReply"/>.</summary>
        public int Id { get; set; }

        public int ReviewId { get; set; }

        public int DirectoryEntryId { get; set; }

        public string? EntryName { get; set; }

        public string? EntryKey { get; set; }

        public DateTime CreateDate { get; set; }

        public byte? Rating { get; set; }

        public ReviewModerationStatus Status { get; set; }

        public string? Body { get; set; }

        public bool HasOrderProof { get; set; }

        public string Excerpt => string.IsNullOrEmpty(this.Body)
            ? string.Empty
            : this.Body.Length > 240 ? this.Body[..240] + "…" : this.Body;

        public string StatusLabel => this.Status switch
        {
            ReviewModerationStatus.Approved => "Published",
            ReviewModerationStatus.Pending => "Waiting",
            ReviewModerationStatus.Flagged => "Flagged",
            ReviewModerationStatus.Rejected => "Rejected",
            _ => this.Status.ToString(),
        };
    }
}
