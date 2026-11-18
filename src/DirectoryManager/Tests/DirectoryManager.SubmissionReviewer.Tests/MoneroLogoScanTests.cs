using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class MoneroLogoScanTests
    {
        [Fact]
        public void FindsImageByFileNameOrAlt()
        {
            var lines = MoneroLogoScan.InHtml("""
                <div class="pay"><img src="/img/coins/xmr.svg" alt="Monero"><img src="/img/visa.png" alt="Visa">
                <img data-src="https://cdn.example/a1b2.png" alt="We accept XMR"></div>
                """);
            Assert.Equal(2, lines.Count);
            Assert.Contains("src=\"/img/coins/xmr.svg\"", lines[0]);
            Assert.Contains("alt=\"Monero\"", lines[0]);
            Assert.Contains("We accept XMR", lines[1]);
        }

        [Fact]
        public void FindsInlineSvg_IconFont_AndCssBackground()
        {
            var lines = MoneroLogoScan.InHtml("""
                <svg class="h-6" viewBox="0 0 10 10"><title>Monero</title><path d="M0 0"/></svg>
                <svg><use href="/sprite.svg#icon-xmr"></use></svg>
                <i class="cc XMR"></i>
                <span style="background-image:url('/assets/monero-logo.png')"></span>
                """);
            Assert.Contains(lines, l => l.StartsWith("inline SVG icon") && l.Contains("Monero"));
            Assert.Contains(lines, l => l.Contains("#icon-xmr"));
            Assert.Contains(lines, l => l.StartsWith("icon element") && l.Contains("cc XMR"));
            Assert.Contains(lines, l => l.StartsWith("CSS background image") && l.Contains("monero-logo.png"));
        }

        [Fact]
        public void IgnoresTextScriptsLayoutClassesAndLookalikes()
        {
            var lines = MoneroLogoScan.InHtml("""
                <p>We love Monero and XMR.</p><a href="/monero">Monero</a>
                <script>var a = "/img/xmr.svg";</script><!-- <img src="xmr.png"> -->
                <div class="monero-section-wrapper"></div>
                <img src="/img/boxmrk.png" alt="Box mark"><img src="/img/btc.svg" alt="Bitcoin">
                """);
            Assert.Empty(lines);
        }

        [Fact]
        public void EmbeddedImageDataIsNotDumped()
        {
            var lines = MoneroLogoScan.InHtml("<img alt=\"Monero\" src=\"data:image/png;base64," + new string('A', 5000) + "\">");
            Assert.Single(lines);
            Assert.True(lines[0].Length < 200);
        }

        [Fact]
        public void AssetsInScript_FindsImageFilesOnly()
        {
            var js = "const coins=[{n:\"btc\",i:\"/assets/btc-1a2b.svg\"},{n:\"xmr\",i:\"/assets/xmr-9f8e.svg\"}];fetch(\"/api/monero/rate\")";
            Assert.Equal(new[] { "image file: \"/assets/xmr-9f8e.svg\"" }, MoneroLogoScan.AssetsInScript(js));
        }

        [Fact]
        public void EmptyInputIsSafe()
        {
            Assert.Empty(MoneroLogoScan.InHtml(null));
            Assert.Empty(MoneroLogoScan.AssetsInScript(string.Empty));
        }
    }
}
