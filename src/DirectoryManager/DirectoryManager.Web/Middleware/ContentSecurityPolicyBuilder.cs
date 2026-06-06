using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Middleware
{
    /// <summary>
    /// Builds the Content-Security-Policy string. Separated from the middleware so the
    /// security-relevant policy can be unit-tested directly (no server/pipeline needed).
    /// </summary>
    public static class ContentSecurityPolicyBuilder
    {
        /// <param name="noScript">
        /// When true, emit <c>script-src 'none'</c> and ignore the nonce / ExtraScriptSources —
        /// the browser refuses ALL JavaScript (external, inline, and on*= handlers). Used to
        /// force a no-JavaScript surface for the authenticated admin area.
        /// </param>
        public static string Build(SecurityHeadersOptions options, string nonce, bool noScript = false)
        {
            var scriptSrc = noScript
                ? "script-src 'none'"
                : Join($"script-src 'self' 'nonce-{nonce}'", options.ExtraScriptSources);

            var directives = new List<string>
            {
                "default-src 'self'",
                "base-uri 'self'",
                "object-src 'none'",
                "frame-ancestors 'none'",
                "frame-src 'none'",
                "form-action 'self'",
                scriptSrc,
                Join("style-src 'self' 'unsafe-inline'", options.ExtraStyleSources),
                Join("img-src 'self' data: https:", options.ExtraImageSources),
                Join("font-src 'self' data:", options.ExtraFontSources),
                Join("connect-src 'self'", options.ExtraConnectSources),
            };

            if (!string.IsNullOrWhiteSpace(options.ReportUri))
            {
                directives.Add($"report-uri {options.ReportUri}");
            }

            return string.Join("; ", directives);
        }

        private static string Join(string baseline, IEnumerable<string> extras)
        {
            var extra = string.Join(" ", extras.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(extra) ? baseline : $"{baseline} {extra}";
        }
    }
}