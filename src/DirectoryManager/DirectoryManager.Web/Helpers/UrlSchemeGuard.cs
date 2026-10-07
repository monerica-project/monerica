namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Render-time guard so a stored link can only ever produce an http(s) (or site-relative)
    /// href. HTML-attribute encoding alone does NOT neutralize a <c>javascript:</c>/<c>data:</c>
    /// scheme (there are no special characters to encode), so output must not depend solely on
    /// the input filter. Defense-in-depth alongside InputHtmlGuard, which already rejects those
    /// schemes on submit/edit.
    /// </summary>
    public static class UrlSchemeGuard
    {
        /// <summary>
        /// Returns the URL only if it is a safe absolute http/https URL, or a site-relative path
        /// ("/..."), after trimming. Returns <c>null</c> for anything else (javascript:, data:,
        /// vbscript:, file:, protocol-relative //host, blank, …), so a dangerous value renders as
        /// a dead/empty href rather than a clickable payload.
        /// </summary>
        public static string? SafeHref(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var u = url.Trim();

            // Site-relative path (but NOT protocol-relative "//host").
            if (u.StartsWith('/') && !u.StartsWith("//", StringComparison.Ordinal))
            {
                return u;
            }

            if (Uri.TryCreate(u, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            {
                return u;
            }

            return null;
        }

        /// <summary>True when <paramref name="url"/> is a safe http(s)/site-relative link.</summary>
        public static bool IsSafeHref(string? url) => SafeHref(url) is not null;
    }
}
