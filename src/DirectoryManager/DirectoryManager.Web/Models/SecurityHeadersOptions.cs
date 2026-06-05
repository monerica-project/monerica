namespace DirectoryManager.Web.Models
{
    /// <summary>
    /// Configuration for <see cref="DirectoryManager.Web.Middleware.SecurityHeadersMiddleware"/>.
    /// Bound from the "SecurityHeaders" config section.
    /// </summary>
    public sealed class SecurityHeadersOptions
    {
        public const string SectionName = "SecurityHeaders";

        /// <summary>
        /// When true, the policy is sent as Content-Security-Policy-Report-Only — browsers
        /// report violations but do NOT block. Use this first in production to find anything
        /// your own pages/snippets would break, then flip to false to enforce.
        /// </summary>
        public bool ContentSecurityPolicyReportOnly { get; set; } = true;

        /// <summary>Optional endpoint browsers POST CSP violation reports to (report-uri).</summary>
        public string? ReportUri { get; set; }

        /// <summary>Extra hosts to allow for scripts (e.g. a CDN). 'self' + per-request nonce are always included.</summary>
        public string[] ExtraScriptSources { get; set; } = System.Array.Empty<string>();

        /// <summary>Extra hosts to allow for styles. 'self' 'unsafe-inline' are always included.</summary>
        public string[] ExtraStyleSources { get; set; } = System.Array.Empty<string>();

        /// <summary>Extra hosts to allow for images. 'self' data: https: are always included.</summary>
        public string[] ExtraImageSources { get; set; } = System.Array.Empty<string>();

        /// <summary>Extra hosts to allow for connect/fetch/XHR/SSE. 'self' is always included.</summary>
        public string[] ExtraConnectSources { get; set; } = System.Array.Empty<string>();

        /// <summary>Extra hosts to allow for fonts. 'self' data: are always included.</summary>
        public string[] ExtraFontSources { get; set; } = System.Array.Empty<string>();
    }
}
