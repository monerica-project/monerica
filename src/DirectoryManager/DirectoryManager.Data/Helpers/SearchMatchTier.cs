namespace DirectoryManager.Data.Helpers
{
    /// <summary>How well a listing's text matches the search. Higher is better.</summary>
    public enum SearchMatchTier
    {
        /// <summary>Only some of the words matched (used when nothing matches them all).</summary>
        Partial = 0,

        /// <summary>Every word matched, but only in the description, note, location or processor.</summary>
        Text = 1,

        /// <summary>Every word matched, at least one only in the broad category name or the domain
        /// (every VPN is in "VPNs &amp; Proxies", so that alone is a weak match for "proxy").</summary>
        Category = 2,

        /// <summary>Every word matched the subcategory or tags (or the name, for a generic search).</summary>
        Taxonomy = 3,

        /// <summary>Every word appears in the name, at least one only inside a longer word ("vps" in "BTCVPS").</summary>
        NameContains = 4,

        /// <summary>Every word is a whole word of the name.</summary>
        NameWord = 5,

        /// <summary>The name starts with the whole search ("trocador" → "Trocador Gift Cards").</summary>
        NamePrefix = 6,

        /// <summary>The search is the listing's name ("goymail", "XMR.CARDS", "solvocard" for SolvoCard.com).</summary>
        ExactName = 7,
    }
}
