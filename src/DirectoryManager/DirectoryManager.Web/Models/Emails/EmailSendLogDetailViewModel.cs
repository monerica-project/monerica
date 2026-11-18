using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Web.Models.Emails
{
    public class EmailSendLogDetailViewModel
    {
        required public EmailSendLog Log { get; init; }

        /// <summary>Null when the content was never stored (older send, storage failure) or was pruned.</summary>
        public EmailSendLogContent? Content { get; init; }

        /// <summary>Other send-log rows that used the same stored content.</summary>
        public int OtherSendsWithSameContent { get; init; }

        public int RetentionDays { get; init; }
    }
}
