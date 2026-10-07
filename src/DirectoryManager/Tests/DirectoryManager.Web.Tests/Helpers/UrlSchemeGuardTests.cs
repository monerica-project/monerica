using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>
    /// Render-time link guard: only http(s) or site-relative URLs may become an href, so a
    /// stored javascript:/data: value can never render as a clickable payload (defense-in-depth
    /// on top of the input-side InputHtmlGuard).
    /// </summary>
    public class UrlSchemeGuardTests
    {
        [Theory] // HAPPY: safe schemes + site-relative paths pass through.
        [InlineData("https://example.com")]
        [InlineData("http://example.com/path?q=1#frag")]
        [InlineData("HTTPS://Example.com")]
        [InlineData("/site/some-listing")]
        [InlineData("/tagged/privacy")]
        public void SafeHref_AllowsHttpAndSiteRelative(string url)
        {
            Assert.NotNull(UrlSchemeGuard.SafeHref(url));
            Assert.True(UrlSchemeGuard.IsSafeHref(url));
        }

        [Theory] // SAD: dangerous schemes, protocol-relative, and blanks are rejected.
        [InlineData("javascript:alert(1)")]
        [InlineData("JavaScript:alert(1)")]
        [InlineData("  javascript:alert(1)  ")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("vbscript:msgbox(1)")]
        [InlineData("file:///etc/passwd")]
        [InlineData("//evil.example.com")] // protocol-relative → would inherit the page scheme
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void SafeHref_RejectsDangerousOrEmpty(string? url)
        {
            Assert.Null(UrlSchemeGuard.SafeHref(url));
            Assert.False(UrlSchemeGuard.IsSafeHref(url));
        }

        [Fact] // HAPPY: surrounding whitespace is trimmed on the returned href.
        public void SafeHref_TrimsWhitespace()
        {
            Assert.Equal("https://x.io/a", UrlSchemeGuard.SafeHref("   https://x.io/a   "));
        }
    }
}
