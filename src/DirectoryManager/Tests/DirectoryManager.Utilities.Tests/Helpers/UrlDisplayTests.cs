using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    public class UrlDisplayTests
    {
        [Fact]
        public void DecodesCyrillicForDisplay()
        {
            Assert.Equal(
                "https://forum.bits.media/topic/67tax-крипта-фиат",
                UrlHelper.ToDisplayUrl("https://forum.bits.media/topic/67tax-%D0%BA%D1%80%D0%B8%D0%BF%D1%82%D0%B0-%D1%84%D0%B8%D0%B0%D1%82"));
        }

        [Theory]
        [InlineData("https://example.com/plain")]
        [InlineData("")]
        public void LeavesUrlsWithoutEscapesAlone(string url) =>
            Assert.Equal(url, UrlHelper.ToDisplayUrl(url));

        [Theory]
        [InlineData("https://example.com/%E2%80%AEtxt.exe")]  // right-to-left override
        [InlineData("https://example.com/a%E2%80%8Bb")]        // zero-width space
        [InlineData("https://example.com/a%0Ab")]              // newline
        [InlineData("https://example.com/a%20b")]              // space
        [InlineData("https://example.com/%FF%FE")]             // not valid UTF-8
        public void KeepsEncodedWhenDecodingWouldHideOrBreakSomething(string url) =>
            Assert.Equal(url, UrlHelper.ToDisplayUrl(url));
    }
}
