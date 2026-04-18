using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class UrlValidatorTests
    {
        [Theory]
        [InlineData("http://example.onion", true)]
        [InlineData("https://example.onion", true)]
        [InlineData("http://example.onion/somepath", true)]
        [InlineData("http://abcdefghij12345k.onion", true)]
        [InlineData("http://abcdefghij12345k.onion/", true)]
        [InlineData("http://abcdefghij12345k.onion/somepath", true)]
        [InlineData("http://lpn5pb34rpsee3ycqtjf3vzngpibxsvzx4a3kdc3rmavgpbpclvq.b32.i2p/en/prepaidcards/?ref=QOAQCBOx11", true)]
        [InlineData("http://www.google.com", true)]
        [InlineData("https://www.google.com", true)]
        [InlineData("ftp://www.google.com", false)] // Only HTTP and HTTPS are valid
        [InlineData("invalid.url", false)]
        [InlineData("http://", false)]
        [InlineData("", false)]        
        public void TestIsValidUrl(string url, bool expected)
        {
            bool result = UrlHelper.IsValidUrl(url);
            Assert.Equal(expected, result);
        }
    }
}
