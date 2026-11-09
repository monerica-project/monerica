using System.Net;
using System.Net.Sockets;
using DirectoryManager.Web.Services.Implementations;
using Xunit;

namespace DirectoryManager.Web.Tests.Services
{
    public class SubmissionLinkCheckerTests
    {
        private static readonly IPAddress PublicIp = IPAddress.Parse("93.184.215.14");

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> respond;

            public FakeHandler(Func<HttpResponseMessage> respond) => this.respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(this.respond());
        }

        private static SubmissionLinkChecker Checker(Func<string, IPAddress[]> dns, Func<HttpResponseMessage> http)
            => new((host, ct) => Task.FromResult(dns(host)), new FakeHandler(http));

        private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

        [Fact] // SAD: made-up domain (submission 9405 used https://only.tor)
        public async Task UnknownDomain_IsRejected()
        {
            var c = Checker(_ => throw new SocketException((int)SocketError.HostNotFound), Ok);
            var error = await c.CheckAsync("https://only.tor");
            Assert.NotNull(error);
            Assert.Contains("only.tor", error);
        }

        [Fact] // SAD: name points only at a private address
        public async Task PrivateOnlyAddress_IsRejected()
        {
            var c = Checker(_ => new[] { IPAddress.Parse("10.0.0.5") }, Ok);
            Assert.NotNull(await c.CheckAsync("https://internal.example.com"));
        }

        [Fact] // SAD: domain exists but nothing listens
        public async Task ConnectionRefused_IsRejected()
        {
            var c = Checker(_ => new[] { PublicIp }, () => throw new HttpRequestException("x", new SocketException((int)SocketError.ConnectionRefused)));
            Assert.NotNull(await c.CheckAsync("https://example.com"));
        }

        [Theory] // HAPPY: any HTTP answer means the site exists (bot walls, errors, redirects)
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.MovedPermanently)]
        public async Task AnyHttpResponse_IsValid(HttpStatusCode status)
        {
            var c = Checker(_ => new[] { PublicIp }, () => new HttpResponseMessage(status));
            Assert.Null(await c.CheckAsync("https://example.com"));
        }

        [Fact] // HAPPY (in doubt): DNS server trouble is not proof the domain is missing
        public async Task DnsTryAgain_IsGivenBenefitOfDoubt()
        {
            var c = Checker(_ => throw new SocketException((int)SocketError.TryAgain), Ok);
            Assert.Null(await c.CheckAsync("https://example.com"));
        }

        [Fact] // HAPPY (in doubt): timeouts / TLS failures pass
        public async Task TimeoutOrTls_IsGivenBenefitOfDoubt()
        {
            var c1 = Checker(_ => new[] { PublicIp }, () => throw new TaskCanceledException());
            var c2 = Checker(_ => new[] { PublicIp }, () => throw new HttpRequestException("ssl", new System.Security.Authentication.AuthenticationException()));
            Assert.Null(await c1.CheckAsync("https://example.com"));
            Assert.Null(await c2.CheckAsync("https://example.com"));
        }

        [Fact] // Malformed input is left to the normal URL validation
        public async Task Malformed_ReturnsNull()
        {
            var c = Checker(_ => throw new InvalidOperationException("should not resolve"), Ok);
            Assert.Null(await c.CheckAsync("not a url"));
        }
    }
}
