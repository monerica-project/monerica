namespace DirectoryManager.Data.Enums
{
    public enum ReviewNotificationStatus
    {
        Pending = 1,
        Sent = 2,
        Failed = 3,

        /// <summary>
        /// Claimed and handed to the email provider, but the "Sent" confirmation wasn't saved
        /// (e.g. the DB save failed after sending). Never retried automatically, so an owner is
        /// never emailed twice for the same item.
        /// </summary>
        Sending = 4
    }
}
