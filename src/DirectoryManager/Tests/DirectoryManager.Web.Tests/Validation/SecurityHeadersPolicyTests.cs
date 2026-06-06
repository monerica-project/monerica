using DirectoryManager.Web.Middleware;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// Locks the security-relevant invariants of the live site-wide CSP emitted by
    /// <see cref="SecurityHeadersMiddleware"/>. These guard against a future edit silently
    /// weakening the policy (e.g. allowing inline script or dropping object-src 'none').
    /// </summary>
    public class SecurityHeadersPolicyTests
    {
        private static string Csp => SecurityHeadersMiddleware.Csp;

        [Fact]
        public void Csp_ForbidsAllJavaScript()
        {
            var scriptSrc = Directive("script-src");

            // No JS at all: not 'self', not a nonce, not inline/eval.
            Assert.Equal("script-src 'none'", scriptSrc);
            Assert.DoesNotContain("'self'", scriptSrc);
            Assert.DoesNotContain("nonce-", scriptSrc);
            Assert.DoesNotContain("unsafe-inline", scriptSrc);
            Assert.DoesNotContain("unsafe-eval", scriptSrc);
        }

        [Fact]
        public void Csp_LocksDownObjectFrameBaseAndDefaults()
        {
            Assert.Contains("default-src 'self'", Csp);
            Assert.Contains("base-uri 'self'", Csp);
            Assert.Contains("object-src 'none'", Csp);
            Assert.Contains("frame-ancestors 'none'", Csp);
            Assert.Contains("form-action 'self'", Csp);
            Assert.Contains("connect-src 'self'", Csp);
        }

        [Fact]
        public void Csp_DoesNotUpgradeInsecureRequests_SoOnionHttpStillWorks()
        {
            Assert.DoesNotContain("upgrade-insecure-requests", Csp);
        }

        private static string Directive(string name) =>
            Csp.Split(';')
               .Select(d => d.Trim())
               .First(d => d.StartsWith(name, StringComparison.Ordinal));
    }
}
