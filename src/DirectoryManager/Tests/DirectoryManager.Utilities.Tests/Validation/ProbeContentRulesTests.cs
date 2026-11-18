using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Utilities.Tests.Validation
{
    public class ProbeContentRulesTests
    {
        [Theory]
        [InlineData("Reviewer-claim probe 651 at 040040. BTCVPS test order; signed with our own PGP key.")]
        [InlineData("Owner-identity verification probe 1262. Clean rating-only review posted anonymously.")]
        [InlineData("A-owned reverse IDOR test review padding text QA994 ok")]
        [InlineData("B overwrite test on same slug padding text QA994 zzz")]
        [InlineData("QA SSRF probe listing for link checker.")]
        [InlineData("ZZ{{7*7}}")]
        [InlineData("ZZ${7*7}")]
        [InlineData("ZZ<%=7*7%>")]
        [InlineData("ZZ#{7*7}")]
        [InlineData("Probe7*7")]
        [InlineData("AUDIT472 hijack proof: anonymous revision of an existing listing")]
        [InlineData("plain zz test listing description")]
        [InlineData("Quiet probe listing used to test the anonymous submission flow")]
        [InlineData("i170 IDOR test edit of entry 1713")]
        [InlineData("Captcha Bypass Test 1707")]
        public void Test_text_from_the_november_scan_is_caught(string text)
        {
            Assert.True(ProbeContentRules.LooksLikeTestText(text));
        }

        [Theory]
        [InlineData("Swapped 3250 USDC for XMR, took around 3 minutes to complete which is relatively fast.")]
        [InlineData("I did a small test order first, then a bigger one. Both arrived in 10 minutes.")]
        [InlineData("Solid VPS so far, well priced, and has a nice control panel. Accepted my Monero payment after one confirmation.")]
        [InlineData("Working, Fast. nothing to say, just good")]
        [InlineData("They ran 3 different AML checkers and all were less than 25% risk score.")]
        [InlineData("CPA and Enrolled Agent firm offering tax preparation, bookkeeping and audit support.")]
        [InlineData("Use Monero and receive a 5% discount on all services.")]
        [InlineData("Open since 2019, 24/7 support, 1000+ products.")]
        [InlineData("I contested the result and support fixed it. The latest release is great.")]
        [InlineData("")]
        [InlineData(null)]
        public void Real_text_passes(string? text)
        {
            Assert.False(ProbeContentRules.LooksLikeTestText(text));
        }

        [Theory]
        [InlineData("test")]
        [InlineData("probe")]
        [InlineData("ZZProbe-brace49")]
        [InlineData("ZZTest Listing")]
        [InlineData("zzz probe")]
        [InlineData("qa-ssrf-2130701433-dec")]
        [InlineData("Captcha Bypass Test 1707")]
        [InlineData("Qa Probe Listing")]
        public void Placeholder_names_are_caught(string name)
        {
            Assert.True(ProbeContentRules.LooksLikeTestName(name));
        }

        [Theory]
        [InlineData("Trocador")]
        [InlineData("0kyc")]
        [InlineData("xmr2cex")]
        [InlineData("Matthew Ryan, CPA.")]
        [InlineData("Protest Gear")]
        [InlineData("Foobar Hosting")]
        [InlineData("Zzz Mattress Co")]
        public void Real_names_pass(string name)
        {
            // "Zzz Mattress Co" starts with zz and is refused; every other name here is fine.
            Assert.Equal(name.StartsWith("Zzz", StringComparison.Ordinal), ProbeContentRules.LooksLikeTestName(name));
        }

        [Theory]
        [InlineData("http://2130701433/")]
        [InlineData("http://134744072/")]
        [InlineData("http://203.0.113.9:18081/json_rpc")]
        [InlineData("http://1.1.1.1/")]
        [InlineData("https://[2001:db8::1]/")]
        [InlineData("http://user:pass@shop.realsite.com/")]
        [InlineData("http://node.realsite.com:18089/")]
        [InlineData("https://app.realsite.com:8443/")]
        [InlineData("https://example.com")]
        [InlineData("https://shop.example.org/page")]
        [InlineData("https://something.invalid/")]
        [InlineData("http://localhost/")]
        [InlineData("http://intranet/")]
        public void Probe_shaped_links_are_refused(string url)
        {
            Assert.NotNull(ProbeContentRules.CheckLink(url));
        }

        [Theory]
        [InlineData("https://trocador.app")]
        [InlineData("https://www.matthewryancpa.com/")]
        [InlineData("https://medium.com/@someone/post")]
        [InlineData("https://github.com/TeamPiped/Piped")]
        [InlineData("http://kukfanqnxdq2aopgvpdf73t37a2ehxevkgtjnixjbco6z4olbeewy3qd.onion:18089/get_info")]
        [InlineData("http://kukfanqnxdq2aopgvpdf73t37a2ehxevkgtjnixjbco6z4olbeewy3qd.onion/")]
        [InlineData("http://site.i2p/")]
        [InlineData("https://exampleshop.com/")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not a url")]
        public void Normal_links_pass(string? url)
        {
            Assert.Null(ProbeContentRules.CheckLink(url));
        }

        [Theory]
        [InlineData("probe@example.com", true)]
        [InlineData("qa170@qa-probe-i170.example.com", true)]
        [InlineData("owner@i503.invalid", true)]
        [InlineData("t@example.org", true)]
        [InlineData("jane.doe.cpa@proton.me", false)]
        [InlineData("hello@exampleshop.com", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Reserved_email_domains(string? email, bool expected)
        {
            Assert.Equal(expected, ProbeContentRules.IsReservedEmail(email));
        }

        [Theory]
        [InlineData("http://make-127-0-0-1-and-192-0-2-1-rr.1u.ms/")]
        [InlineData("https://httpbin.org/delay/6")]
        [InlineData("http://httpbin.org:80/")]
        [InlineData("https://httpbin.org:443/")]
        [InlineData("http://shop-example-real.com:0080/")]
        [InlineData("http://shop-example-real.com:/")]
        [InlineData("http://shop-example-real.com.:80/")]
        [InlineData("http://127.0.0.1.xip.io/")]
        [InlineData("https://abc.requestrepo.com/")]
        public void SecondWaveLinks_AreRefused(string url) =>
            Assert.NotNull(ProbeContentRules.CheckLink(url));

        [Theory]
        [InlineData("https://www.matthewryancpa.com/")]
        [InlineData("https://shop.realstore.io/path?x=1")]
        [InlineData("http://abcdefghijklmnopabcdefghijklmnopabcdefghijklmnopabcdefgh.onion:8080/")]
        public void RealLinks_StillPass(string url) =>
            Assert.Null(ProbeContentRules.CheckLink(url));

        [Theory]
        [InlineData("Canary Audit p80")]
        [InlineData("canary")]
        public void CanaryWording_IsRefused(string text) =>
            Assert.True(ProbeContentRules.LooksLikeTestText(text));
    }
}
