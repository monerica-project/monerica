using DirectoryManager.SiteChecker.Helpers;

namespace DirectoryManager.SiteChecker.Tests
{
    // SSRF guard: listing URLs and every redirect they send the checker to must never reach the
    // server's own network. No network access needed: the block happens before any connect.
    public class InternalAddressBlockTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.1.2.3")]
        [InlineData("192.168.0.10")]
        [InlineData("169.254.169.254")]
        [InlineData("::1")]
        [InlineData("localhost")]
        public async Task Internal_hosts_are_refused_before_connecting(string host)
        {
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                HappyEyeballs.ConnectAsync(host, 80, TimeSpan.FromSeconds(2), CancellationToken.None));
            Assert.Contains("Blocked connection to internal address", ex.Message);
        }
    }
}
