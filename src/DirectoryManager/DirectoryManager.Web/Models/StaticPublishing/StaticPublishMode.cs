namespace DirectoryManager.Web.Models.StaticPublishing
{
    /// <summary>Publish-on-change switch (config StaticPublish:Mode).</summary>
    public enum StaticPublishMode
    {
        /// <summary>Nothing is queued and the publisher gets no work (full crawls only).</summary>
        Off = 0,

        /// <summary>Everything runs (queue, fetch, rewrite, compare) but nothing is uploaded or purged;
        /// the log records what WOULD have changed.</summary>
        DryRun = 1,

        /// <summary>Changed pages are published to the static mirror within seconds.</summary>
        On = 2,
    }
}
