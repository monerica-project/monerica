using System.Net;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    // SSRF guard: the site checker and web app must never connect to internal addresses.
    public class NetworkAddressHelperTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("127.8.9.10")]
        [InlineData("10.0.0.5")]
        [InlineData("172.16.0.1")]
        [InlineData("172.31.255.255")]
        [InlineData("192.168.1.1")]
        [InlineData("169.254.169.254")] // cloud metadata
        [InlineData("100.64.0.1")]      // CGNAT
        [InlineData("0.0.0.0")]
        [InlineData("224.0.0.1")]       // multicast
        [InlineData("255.255.255.255")] // broadcast
        [InlineData("::1")]
        [InlineData("::")]
        [InlineData("fe80::1")]         // link-local
        [InlineData("fd12:3456::1")]    // unique-local
        [InlineData("::ffff:192.168.1.1")] // IPv4-mapped private
        [InlineData("::ffff:127.0.0.1")]
        public void Internal_addresses_are_blocked(string address)
        {
            Assert.True(NetworkAddressHelper.IsPrivateOrDisallowed(IPAddress.Parse(address)));
        }

        [Theory]
        [InlineData("1.1.1.1")]
        [InlineData("8.8.8.8")]
        [InlineData("172.15.0.1")]  // just outside 172.16/12
        [InlineData("172.32.0.1")]
        [InlineData("100.63.0.1")]  // just outside CGNAT
        [InlineData("192.169.0.1")]
        [InlineData("2606:4700:4700::1111")]
        [InlineData("::ffff:8.8.8.8")]
        public void Public_addresses_are_allowed(string address)
        {
            Assert.False(NetworkAddressHelper.IsPrivateOrDisallowed(IPAddress.Parse(address)));
        }

        [Fact]
        public void Null_fails_closed()
        {
            Assert.True(NetworkAddressHelper.IsPrivateOrDisallowed(null));
        }
    }
}
