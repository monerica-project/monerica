using DirectoryManager.Web.Middleware;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// Locks the security-relevant invariants of the CSP. These guard against a future
    /// edit silently weakening the policy (e.g. adding 'unsafe-inline' to scripts).
    /// </summary>
    public class ContentSecurityPolicyBuilderTests
    {
        private static SecurityHeadersOptions Defaults() => new ();

        [Fact]
        public void Build_ScriptSrc_UsesNonce_AndNotUnsafeInline()
        {
            var csp = ContentSecurityPolicyBuilder.Build(Defaults(), "abc123");

            Assert.Contains("script-src 'self' 'nonce-abc123'", csp);

            // The script directive must never allow inline execution.
            var scriptDirective = csp.Split(';')
                .First(d => d.TrimStart().StartsWith("script-src", StringComparison.Ordinal));
            Assert.DoesNotContain("unsafe-inline", scriptDirective);
            Assert.DoesNotContain("unsafe-eval", scriptDirective);
        }

        [Fact]
        public void Build_LocksDownObjectFrameAndBase()
        {
            var csp = ContentSecurityPolicyBuilder.Build(Defaults(), "n");

            Assert.Contains("object-src 'none'", csp);
            Assert.Contains("frame-ancestors 'none'", csp);
            Assert.Contains("base-uri 'self'", csp);
            Assert.Contains("default-src 'self'", csp);
        }

        [Fact]
        public void Build_DoesNotUpgradeInsecureRequests_SoOnionHttpStillWorks()
        {
            var csp = ContentSecurityPolicyBuilder.Build(Defaults(), "n");

            Assert.DoesNotContain("upgrade-insecure-requests", csp);
        }

        [Fact]
        public void Build_AppendsExtraSources_WhenConfigured()
        {
            var options = new SecurityHeadersOptions
            {
                ExtraScriptSources = new[] { "https://cdn.example" },
                ExtraConnectSources = new[] { "https://api.example" },
            };

            var csp = ContentSecurityPolicyBuilder.Build(options, "n");

            Assert.Contains("script-src 'self' 'nonce-n' https://cdn.example", csp);
            Assert.Contains("connect-src 'self' https://api.example", csp);
        }

        [Fact]
        public void Build_AddsReportUri_OnlyWhenSet()
        {
            var without = ContentSecurityPolicyBuilder.Build(Defaults(), "n");
            Assert.DoesNotContain("report-uri", without);

            var with = ContentSecurityPolicyBuilder.Build(
                new SecurityHeadersOptions { ReportUri = "/csp-report" }, "n");
            Assert.Contains("report-uri /csp-report", with);
        }
    }
}
