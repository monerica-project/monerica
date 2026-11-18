using DirectoryManager.Data.Models.StaticPublishing;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// What may go into the static-publish queue: a local, root-relative page path. Used for the
    /// admin "Republish this page" box (which also accepts a full monerica.com / app URL) and as a
    /// last check on every enqueue. Pure.
    /// </summary>
    public static class StaticPublishPathRules
    {
        /// <summary>Normalizes <paramref name="input"/> ("/site/x", "site/x/", "https://monerica.com/site/x")
        /// to "/site/x". Only URLs on <paramref name="allowedHosts"/> are accepted.</summary>
        public static bool TryNormalize(string? input, IEnumerable<string> allowedHosts, out string path, out string error)
        {
            path = string.Empty;
            error = string.Empty;
            var raw = (input ?? string.Empty).Trim();
            if (raw.Length == 0)
            {
                error = "Enter a page path such as /site/example.";
                return false;
            }

            if (raw.Length > StaticPublishConstants.MaxPathLength)
            {
                error = "That path is too long.";
                return false;
            }

            if (raw.Contains("://", StringComparison.Ordinal))
            {
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                {
                    error = "That is not a valid address.";
                    return false;
                }

                if (!allowedHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Only pages on this site can be republished.";
                    return false;
                }

                if (!string.IsNullOrEmpty(uri.Query))
                {
                    error = "Pages with a query string are not part of the static site.";
                    return false;
                }

                raw = uri.AbsolutePath;
            }
            else if (raw.StartsWith("//", StringComparison.Ordinal))
            {
                error = "Only pages on this site can be republished.";
                return false;
            }

            // Drop a "#fragment" (never sent to the server).
            var hash = raw.IndexOf('#');
            if (hash >= 0)
            {
                raw = raw[..hash];
            }

            if (!raw.StartsWith('/'))
            {
                raw = "/" + raw;
            }

            if (raw.Length > 1)
            {
                raw = raw.TrimEnd('/');
            }

            if (!IsValidPath(raw, out error))
            {
                return false;
            }

            path = raw;
            return true;
        }

        /// <summary>One leading "/", only slug characters, no "." / ".." segments, no empty segments
        /// (the publisher enforces the same shape on its side).</summary>
        public static bool IsValidPath(string? path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
            {
                error = "A page path must start with a single /.";
                return false;
            }

            if (path.Length > StaticPublishConstants.MaxPathLength)
            {
                error = "That path is too long.";
                return false;
            }

            // Page paths on this site are plain ASCII slugs: letters, digits, "-", "_", ".", "~" and "/".
            foreach (var c in path)
            {
                if (!(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.' or '~'))
                {
                    error = "That path contains a character that is not allowed.";
                    return false;
                }
            }

            var segments = path.Split('/');
            for (var i = 1; i < segments.Length; i++)
            {
                var seg = segments[i];
                if ((seg.Length == 0 && i != segments.Length - 1) || seg == "." || seg == "..")
                {
                    error = "That path is not a plain page path.";
                    return false;
                }
            }

            return true;
        }
    }
}
