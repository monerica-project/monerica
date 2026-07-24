namespace DirectoryManager.SiteChecker.Helpers
{
    /// <summary>
    /// The result of a single link check. Distinguishing <see cref="Inconclusive"/>
    /// from <see cref="Offline"/> is the whole point: a Tor onion that times out or
    /// fails its SOCKS circuit is <see cref="Inconclusive"/> (could just be slow), and
    /// must NOT be treated as down on the strength of one run. Only a server that
    /// answers "gone" is <see cref="Offline"/>.
    /// </summary>
    public enum CheckOutcome
    {
        /// <summary>Server answered in a way that proves it is up (2xx/3xx, or a guard like 403/429/503).</summary>
        Online,

        /// <summary>No usable answer — timeout, Tor circuit/SOCKS failure, connection error. Ambiguous on any single run.</summary>
        Inconclusive,

        /// <summary>Server answered "gone" (404/410/521) — a definitive negative.</summary>
        Offline
    }
}
