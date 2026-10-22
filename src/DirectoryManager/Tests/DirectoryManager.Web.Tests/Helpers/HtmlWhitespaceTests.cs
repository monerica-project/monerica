using DirectoryManager.Web.Middleware;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class HtmlWhitespaceTests
    {
        [Fact]
        public void KeepsBlankLineInsidePgpCiphertextBlock()
        {
            var html = "<p>x</p>\n\n<div class=\"card p-3 u-ws-pre-wrap-ff-lucida-console\">-----BEGIN PGP MESSAGE-----\n\nhQEMA\n-----END PGP MESSAGE-----</div>\n";

            var output = HtmlWhitespace.Collapse(html);

            Assert.Contains("-----BEGIN PGP MESSAGE-----\n\nhQEMA", output);
        }

        [Fact]
        public void KeepsParagraphBreaksInPrewrapReviewText()
        {
            var html = "<div class=\"u-prewrap\">first\n\nsecond</div>";

            Assert.Equal(html, HtmlWhitespace.Collapse(html));
        }

        [Fact]
        public void StillRemovesBlankLinesAndTrailingSpacesElsewhere()
        {
            var html = "<ul>  \n\n   \n<li>a</li>   \n</ul>";

            Assert.Equal("<ul>\n<li>a</li>\n</ul>", HtmlWhitespace.Collapse(html));
        }

        [Fact]
        public void LeavesPreUntouched()
        {
            var html = "<pre>a\n\n  b  \n</pre>";

            Assert.Equal(html, HtmlWhitespace.Collapse(html));
        }
    }
}
