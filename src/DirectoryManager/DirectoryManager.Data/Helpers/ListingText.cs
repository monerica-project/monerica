namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// One set of length limits for listing text, used by the public submission form, the
    /// submission record, the admin edit form and the listing itself, so anything accepted at
    /// the start can always be approved at the end.
    /// Line breaks are stored as a single "\n": browsers post textarea line breaks as "\r\n",
    /// which used to make the same text 1 character longer per line on each round trip.
    /// </summary>
    public static class ListingText
    {
        public const int DescriptionMaxLength = 175;
        public const int NoteMaxLength = 250;
        public const int LinkMaxLength = 500;

        public static string? NormalizeLineBreaks(string? value) =>
            value?.Replace("\r\n", "\n").Replace('\r', '\n');

        /// <summary>
        /// Listings never shout: "Fast swaps!" → "Fast swaps." and "Great!!! Easy" → "Great. Easy".
        /// A "!" inside a word or URL (e.g. "Yahoo!Mail", "/#!/page") is left alone.
        /// </summary>
        public static string? RemoveExclamations(string? value)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains('!'))
            {
                return value;
            }

            var s = System.Text.RegularExpressions.Regex.Replace(value, @"[!¡]+(?=\s|$)", ".");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"([.?])\.+", "$1");
            return s.Trim();
        }
    }
}
