using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class ReferralScanTests
    {
        private static readonly Uri Page = new Uri("https://okspot.com/");

        // Shape of OKSPOT's page: the server HTML footer has no referral link; the menu is in the JS bundle.
        private const string SpaHtml = """
            <html><head>
            <script type="module" crossorigin src="/assets/index-C0XM-E6t.js"></script>
            <link rel="modulepreload" href="/assets/vendor-abc.js">
            <script src="https://cdn.other.example/lib.js"></script>
            </head><body><a href="/help">Help Center</a> <a href="/legal">Legal</a></body></html>
            """;

        private const string SpaScript = """
            const Wj=[["/referrals","Referral"],["/help","Help Center"],["/fees","Fees"]];
            const icons={"affiliates-12-active":"x","affiliates-16-active":"y"};
            fetch("/api/referrals/summary");
            """;

        [Fact]
        public void ScriptUrls_SameSiteOnly_InPageOrder()
        {
            var urls = ReferralScan.ScriptUrls(SpaHtml, Page);
            Assert.Equal(new[] { "https://okspot.com/assets/index-C0XM-E6t.js", "https://okspot.com/assets/vendor-abc.js" }, urls);
        }

        [Fact]
        public void RoutesInScript_FindsReferralRoute_NotIconNamesOrApiPaths()
        {
            var routes = ReferralScan.RoutesInScript(SpaScript);
            Assert.Equal(new[] { "/referrals" }, routes);
        }

        [Theory]
        [InlineData("\"/affiliate\"", "/affiliate")]
        [InlineData("'/affiliate-program'", "/affiliate-program")]
        [InlineData("`/refer-a-friend`", "/refer-a-friend")]
        [InlineData("\"/partner-program/apply\"", "/partner-program/apply")]
        public void RoutesInScript_CommonProgramPaths(string js, string expected)
        {
            Assert.Contains(expected, ReferralScan.RoutesInScript(js));
        }

        [Theory]
        [InlineData("\"/earn\"")]
        [InlineData("\"/markets\"")]
        [InlineData("\"/referrer-policy-docs\"")]
        [InlineData("\"affiliates-12-active\"")]
        public void RoutesInScript_IgnoresUnrelated(string js)
        {
            Assert.Empty(ReferralScan.RoutesInScript(js));
        }

        [Fact]
        public void LinksInHtml_FindsByUrlOrText()
        {
            const string html = """
                <a href="/ref/program">Earn with us</a>
                <a href="/affiliates">Partners</a>
                <a href="/about">About</a>
                <a href="/x">Referral Program</a>
                """;
            var links = ReferralScan.LinksInHtml(html, Page);
            Assert.Equal(3, links.Count);
            Assert.DoesNotContain(links, l => l.Contains("About", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FindReferralProgramAsync_ReadsSiteScript()
        {
            var fetcher = new FakeFetcher(new Dictionary<string, string>
            {
                ["https://okspot.com/assets/index-C0XM-E6t.js"] = SpaScript,
            });
            var builder = new SubmissionDossierBuilder(fetcher);
            var main = new FetchResult(Page.ToString(), true, 200, SpaHtml, null, Page);

            var evidence = await builder.FindReferralProgramAsync(main, CancellationToken.None);

            Assert.Single(evidence);
            Assert.Contains("/referrals", evidence[0], StringComparison.Ordinal);
        }

        [Fact]
        public void Build_AddsReferralSection_OnlyWhenEvidence()
        {
            var s = new Submission { Name = "OKSPOT", Link = "https://okspot.com" };
            var page = new FetchResult(Page.ToString(), true, 200, SpaHtml, null, Page);

            var with = SubmissionDossierBuilder.Build(s, null, page, null, null, Array.Empty<ExistingListing>(), 6000, new[] { "route /referrals" });
            var without = SubmissionDossierBuilder.Build(s, null, page, null, null, Array.Empty<ExistingListing>(), 6000);

            Assert.Contains("## Referral / affiliate program check", with, StringComparison.Ordinal);
            Assert.DoesNotContain("Referral / affiliate program check", without, StringComparison.Ordinal);
        }

        private sealed class FakeFetcher : IPageFetcher
        {
            private readonly Dictionary<string, string> bodies;

            public FakeFetcher(Dictionary<string, string> bodies) => this.bodies = bodies;

            public Task<FetchResult> FetchAsync(string url, CancellationToken ct) =>
                Task.FromResult(this.bodies.TryGetValue(url, out var body)
                    ? new FetchResult(url, true, 200, body, null, new Uri(url))
                    : new FetchResult(url, true, 404, null, "HTTP 404", new Uri(url)));
        }
    }
}
