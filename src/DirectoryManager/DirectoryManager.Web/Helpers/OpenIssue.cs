using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>A listing whose current status is Scam or Questionable: an open issue.</summary>
    public sealed class OpenIssue
    {
        public int DirectoryEntryId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string DirectoryEntryKey { get; init; } = string.Empty;

        /// <summary>The current status: Scam or Questionable.</summary>
        public DirectoryStatus Status { get; init; }

        /// <summary>When the listing entered its current status.</summary>
        public DateTime SinceUtc { get; init; }

        /// <summary>The note saved when it entered that status, else the current note (null = none).</summary>
        public string? Reason { get; init; }

        /// <summary>The listing's main website (null = none).</summary>
        public string? Website { get; init; }
    }
}
