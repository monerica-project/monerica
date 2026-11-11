using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class SiteSignalScanTests
    {
        private static readonly Uri Page = new Uri("https://swapdusk.com/");

        // Shape of SwapDusk's footer (submission 9403): API docs + terms links, "No KYC" in the hero.
        private const string Html = """
            <html><body>
            <h1>Swap crypto, no account, no KYC.</h1>
            <p>Swap any coin for any other. No account, no KYC, no registration. See every fee before you send.</p>
            <footer><a href="/api-docs/">API docs</a> <a href="terms.html">Terms</a>
            <a href="/swap/monero-xmr/">Monero</a> <a href="https://t.me/x">Telegram</a> <a href="/apiary-honey">Honey</a></footer>
            </body></html>
            """;

        private const string Terms = """
            <html><body><h1>Terms</h1><p>We don't hold accounts or balances for you.
            We may hold or refuse transactions linked to fraud, theft or sanctions. Refunds go to your refund address.</p></body></html>
            """;

        [Fact]
        public void ApiLinks_FindsApiDocs_NotLookalikes()
        {
            var links = SiteSignalScan.ApiLinks(Html, Page);
            Assert.Equal(new[] { "API docs (https://swapdusk.com/api-docs/)" }, links);
        }

        [Fact]
        public void LegalPages_SubmittedLink2First_ThenTermsLinks_Deduped()
        {
            var pages = SiteSignalScan.LegalPages(Html, Page, new[] { "https://swapdusk.com/terms.html", null });
            Assert.Equal(new[] { "https://swapdusk.com/terms.html" }, pages);
        }

        [Fact]
        public void LegalPages_IgnoresOtherDomains()
        {
            var pages = SiteSignalScan.LegalPages(Html, Page, new[] { "https://other.example/terms" });
            Assert.DoesNotContain(pages, p => p.Contains("other.example"));
        }

        [Fact]
        public void KycSentences_FindsNoKycAndHoldClause()
        {
            Assert.Contains(SiteSignalScan.KycSentences(Html), s => s.Contains("no KYC"));
            var terms = SiteSignalScan.KycSentences(Terms);
            Assert.Contains(terms, s => s.Contains("hold or refuse transactions"));
            Assert.DoesNotContain(terms, s => s.Contains("Refunds go"));
        }
    }
}
