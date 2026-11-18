using DirectoryManager.Web.Helpers;
using Xunit;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>The Tor box takes only .onion addresses and the I2P box only .i2p addresses.</summary>
    public class TorAndI2pFieldTests
    {
        [Theory]
        [InlineData("http://exampleonionaddressxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.onion", true)]
        [InlineData("http://abc.onion/shop?x=1", true)]
        [InlineData("HTTP://ABC.ONION:8080/", true)]
        [InlineData("https://swapdusk.com/terms.html", false)]
        [InlineData("https://example.com/abc.onion", false)]
        [InlineData("https://abc.onion.example.com/", false)]
        [InlineData("http://abc.i2p", false)]
        [InlineData("abc.onion", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsOnion_OnlyForOnionHosts(string? url, bool expected)
        {
            Assert.Equal(expected, UrlHelper.IsOnion(url));
        }

        [Theory]
        [InlineData("http://exampleabc.b32.i2p/", true)]
        [InlineData("http://shop.i2p/page", true)]
        [InlineData("https://nordbastion.com/dedicated/", false)]
        [InlineData("http://abc.onion", false)]
        [InlineData("https://example.com/?to=x.i2p", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsI2p_OnlyForI2pHosts(string? url, bool expected)
        {
            Assert.Equal(expected, UrlHelper.IsI2p(url));
        }
    }
}
