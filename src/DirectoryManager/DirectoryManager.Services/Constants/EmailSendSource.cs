namespace DirectoryManager.Services.Constants
{
    /// <summary>
    /// Canonical source identifiers written to EmailSendLog.SourceApplication.
    /// Each service constructs its EmailService with the matching value so the
    /// audit log records which application sent each message.
    /// </summary>
    public static class EmailSendSource
    {
        public const string Web = "Web";
        public const string NewsletterSender = "NewsletterSender";
        public const string SponsoredListingOpening = "SponsoredListingOpening";
        public const string SponsoredListingReminder = "SponsoredListingReminder";
        public const string Unknown = "Unknown";
    }
}
