namespace DirectoryManager.Data.Helpers
{
    /// <summary>The listing page's restrictions blurb (see <see cref="ListingRestrictions.Summarize"/>).</summary>
    public sealed class RestrictionsSummary
    {
        /// <summary>The one-line summary, e.g. "14 countries not served · VPN not allowed". Empty when nothing is recorded.</summary>
        public string SummaryLine { get; init; } = string.Empty;

        /// <summary>Countries not served, by name, A–Z.</summary>
        public IReadOnlyList<string> CountryNames { get; init; } = Array.Empty<string>();

        /// <summary>"Allowed" / "Not allowed" / "May trigger verification", or null when not stated.</summary>
        public string? Vpn { get; init; }

        /// <summary>Same as <see cref="Vpn"/>, for Tor.</summary>
        public string? Tor { get; init; }

        /// <summary>The "other restrictions" text (plain; encode on output).</summary>
        public string? Other { get; init; }

        /// <summary>True when the summary line leaves something out (long country list or long "other" text), so it should expand.</summary>
        public bool NeedsDetails { get; init; }

        public bool IsEmpty => this.SummaryLine.Length == 0;
    }
}
