namespace DirectoryManager.Web.Helpers
{
    /// <summary>How review and reply authors are shown in bylines.</summary>
    public static class AuthorDisplay
    {
        /// <summary>
        /// A 40-character PGP fingerprint is noise in a byline: show "7C22…1A25" (the full value goes
        /// in a title attribute for hover). Short values are returned unchanged.
        /// </summary>
        public static string ShortFingerprint(string? fingerprint)
        {
            var v = (fingerprint ?? string.Empty).Trim();
            return v.Length > 12 ? $"{v.Substring(0, 4)}…{v.Substring(v.Length - 4)}" : v;
        }
    }
}
