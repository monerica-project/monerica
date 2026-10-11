namespace DirectoryManager.Data.Enums
{
    public enum VideoIdeaStatus
    {
        Unknown = 0,

        // In the queue: not made yet, shown on the public upcoming list.
        Queued = 1,

        // The video has been published.
        Completed = 2,

        // Dropped from the queue (hidden everywhere public).
        Retired = 3,
    }
}
