namespace DirectoryManager.Web.Helpers
{
    public static class LinkVariationHelper
    {
        /// <summary>
        /// Builds URL candidates toggling www/no-www, trailing slash/no trailing slash,
        /// and both http/https. Ignores query and fragment for comparison.
        /// Also emits BOTH root forms ("https://host" and "https://host/") so exact-string
        /// DB matches succeed regardless of how the link was stored.
        /// </summary>
        /// <returns>A list of variations.</returns>
        public static IEnumerable<string> GenerateLinkVariants(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                yield break;
            }

            static Uri CoerceToAbsolute(string url)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var u))
                {
                    return u;
                }

                var httpsTry = "https://" + url.TrimStart('/');
                if (Uri.TryCreate(httpsTry, UriKind.Absolute, out var u2))
                {
                    return u2;
                }

                var httpTry = "http://" + url.TrimStart('/');
                return new Uri(httpTry, UriKind.Absolute);
            }

            static int NormalizePort(string scheme, int port)
            {
                if (port < 0)
                {
                    return -1;
                }

                if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && port == 80)
                {
                    return -1;
                }

                if (scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && port == 443)
                {
                    return -1;
                }

                return port;
            }

            var original = CoerceToAbsolute(input);

            // host base (strip www.)
            var baseHost = original.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? original.Host[4..]
                : original.Host;

            // path normalization
            bool isRootPath = string.IsNullOrEmpty(original.AbsolutePath) || original.AbsolutePath == "/";
            string pathNoTrailing = isRootPath
                ? "" // IMPORTANT: empty means "no slash at all" for root variant
                : (original.AbsolutePath.EndsWith("/") ? original.AbsolutePath.TrimEnd('/') : original.AbsolutePath);

            string pathWithTrailing = isRootPath ? "/" : (pathNoTrailing + "/");

            var schemes = new[] { "https", "http" };
            var hosts = new[]
            {
                baseHost.ToLowerInvariant(),
                ("www." + baseHost).ToLowerInvariant()
            };

            var originalPort = original.IsDefaultPort ? -1 : original.Port;

            foreach (var scheme in schemes)
            {
                foreach (var host in hosts)
                {
                    var port = NormalizePort(scheme, originalPort);

                    // 1) WITH trailing slash
                    var ub = new UriBuilder(scheme, host)
                    {
                        Path = pathWithTrailing,
                        Query = string.Empty,
                        Fragment = string.Empty,
                        Port = port
                    };
                    yield return ub.Uri.AbsoluteUri;

                    // 2) WITHOUT trailing slash
                    //    root: NO slash at all (e.g., https://host)
                    //    non-root: path without trailing slash (e.g., https://host/path)
                    var portPart = port > 0 ? $":{port}" : string.Empty;
                    var pathPart = isRootPath ? "" : pathNoTrailing; // keep leading '/'
                    yield return $"{scheme}://{host}{portPart}{pathPart}";
                }
            }
        }
    }
}
