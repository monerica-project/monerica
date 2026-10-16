namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Single source of truth for which <see cref="DirectoryStatus"/> values count as a live,
    /// promotable listing (the "live advertiser" rule): Admitted or Verified. Removed, Scam,
    /// Questionable and Unknown listings are never promoted as sponsors or featured.
    /// </summary>
    public static class DirectoryStatusExtensions
    {
        public static bool IsLive(this DirectoryStatus status) =>
            status == DirectoryStatus.Admitted || status == DirectoryStatus.Verified;
    }
}
