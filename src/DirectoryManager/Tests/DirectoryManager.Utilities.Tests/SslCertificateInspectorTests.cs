using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Utilities.Tests
{
    public class SslCertificateInspectorTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        private static readonly X509ChainStatusFlags[] NoChainIssues = Array.Empty<X509ChainStatusFlags>();

        [Fact]
        public void Describe_HealthyCertificate_ReturnsNull()
        {
            Assert.Null(SslCertificateInspector.Describe("example.com", Now.AddDays(-30), Now.AddDays(60), SslPolicyErrors.None, NoChainIssues, "example.com", Now));
        }

        [Fact]
        public void Describe_Expired_SaysWhen()
        {
            var issue = SslCertificateInspector.Describe("example.com", Now.AddDays(-90), new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), SslPolicyErrors.RemoteCertificateChainErrors, NoChainIssues, "example.com", Now);

            Assert.Equal("expired on 2026-10-03", issue);
        }

        [Fact]
        public void Describe_NotYetValid_SaysWhen()
        {
            var issue = SslCertificateInspector.Describe("example.com", Now.AddDays(2), Now.AddDays(90), SslPolicyErrors.RemoteCertificateChainErrors, NoChainIssues, "example.com", Now);

            Assert.StartsWith("not valid until", issue);
        }

        [Fact]
        public void Describe_WrongDomain_NamesBothDomains()
        {
            var issue = SslCertificateInspector.Describe("example.com", Now.AddDays(-30), Now.AddDays(60), SslPolicyErrors.RemoteCertificateNameMismatch, NoChainIssues, "other.net", Now);

            Assert.Equal("issued for a different domain (other.net, not example.com)", issue);
        }

        [Fact]
        public void Describe_SelfSigned_IsNotTrusted()
        {
            var issue = SslCertificateInspector.Describe("example.com", Now.AddDays(-30), Now.AddDays(60), SslPolicyErrors.RemoteCertificateChainErrors, new[] { X509ChainStatusFlags.UntrustedRoot }, "example.com", Now);

            Assert.Equal("not trusted (self-signed or unknown issuer)", issue);
        }

        // An incomplete chain or an unreachable revocation server is not something a visitor
        // normally sees, so it must not create a flag.
        [Theory]
        [InlineData(X509ChainStatusFlags.PartialChain)]
        [InlineData(X509ChainStatusFlags.RevocationStatusUnknown)]
        [InlineData(X509ChainStatusFlags.OfflineRevocation)]
        public void Describe_ChainNoiseOnly_ReturnsNull(X509ChainStatusFlags status)
        {
            Assert.Null(SslCertificateInspector.Describe("example.com", Now.AddDays(-30), Now.AddDays(60), SslPolicyErrors.RemoteCertificateChainErrors, new[] { status }, "example.com", Now));
        }

        [Fact]
        public async Task InspectAsync_NonHttpsUrl_IsNotChecked()
        {
            var (isChecked, issue) = await SslCertificateInspector.InspectAsync("http://example.com", TimeSpan.FromSeconds(1));

            Assert.False(isChecked);
            Assert.Null(issue);
        }
    }
}
