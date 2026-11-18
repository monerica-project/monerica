namespace DirectoryManager.Web.Models.Emails
{
    /// <summary>Config section "EmailSendLog" (optional; defaults apply when absent).</summary>
    public class EmailSendLogOptions
    {
        public const string SectionName = "EmailSendLog";

        public const int DefaultContentRetentionDays = 90;

        /// <summary>Stored message content not used by a send for this many days is deleted
        /// (the send-log rows themselves are kept).</summary>
        public int ContentRetentionDays { get; set; } = DefaultContentRetentionDays;

        public int EffectiveRetentionDays => this.ContentRetentionDays > 0 ? this.ContentRetentionDays : DefaultContentRetentionDays;
    }
}
