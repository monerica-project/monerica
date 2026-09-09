using System;
using System.Linq;
using System.Text;
using DirectoryManager.Web.Helpers;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Bcpg.Sig;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace DirectoryManager.Web.Tests.Services
{
    /// <summary>
    /// Tests for PgpCapabilities: the shared logic behind the ownership challenge and the
    /// upload-time capability check (fixes #1a and #2 of the pablo.cash PGP report).
    /// </summary>
    public class PgpCapabilitiesTests
    {
        // pablo.cash's real, published, updated public key: ed25519 [C] primary + [S] signing subkey
        // + cv25519 [E] encryption subkey (added 2026-09-05). Public material only.
        private const string PabloUpdatedKey =
            "-----BEGIN PGP PUBLIC KEY BLOCK-----\n\n" +
            "mDMEapW8dxYJKwYBBAHaRw8BAQdAlkUYVJGBUiLnzUtbYxgwdSkiXLWWczP5jgbQ\n" +
            "qKWLykq0JVBhYmxvIENhc2ggT3JkZXIgU2lnbmluZyAocGFibG8uY2FzaCmIkAQT\n" +
            "FgoAOAIbAQULCQgHAgYVCgkICwIEFgIDAQIeAQIXgBYhBD7CiyfNMDsrsr0Y55Qp\n" +
            "fEXGgqQJBQJqlb5+AAoJEJQpfEXGgqQJ/ScA/j2slL0ppKRcANTITu7xjn9YpVse\n" +
            "+u1UqtilO9/6um7eAP0axKrmyiZUoNOheLlR9s+6OzUel9BgTi3jvChmxeKkBLgz\n" +
            "BGqVvHcWCSsGAQQB2kcPAQEHQEZpj+/S26KeC/SgIlJP48zZBbrGSaCCwx3AL+le\n" +
            "HA3oiO8EGBYKACACGwIWIQQ+wosnzTA7K7K9GOeUKXxFxoKkCQUCapW+dwCBdiAE\n" +
            "GRYKAB0WIQQG6O64ydNij07M4dv1K6et1XYGIgUCapW8dwAKCRD1K6et1XYGIo8s\n" +
            "AP9dCodsiebs1beRz9aVOwDIUprLIIeLJBjcQjT2VUBC1gEA+bstCQGB/tOqXxbZ\n" +
            "pTlag/Hjqnkd+hwaDvGe+WSKEwwJEJQpfEXGgqQJhxcBAOogmD3m5OWh6sUUG6bv\n" +
            "WalJWom1sBZa5t3gvv6la2yjAP9A7uRWn2JSldUC/LPCajPmB1jrMAKH4/pqiCkY\n" +
            "nUMIB7g4BGqbUKASCisGAQQBl1UBBQEBB0DC2Li4Q2eiPvbKiYB8Ym4n+/khbPpl\n" +
            "3o1muLdbU8YuMAMBCAeIeAQYFgoAIBYhBD7CiyfNMDsrsr0Y55QpfEXGgqQJBQJq\n" +
            "m1CgAhsMAAoJEJQpfEXGgqQJdwgA/iILw/+ZOYMKZvvg/ORsQLDxb8c1gGBEogPe\n" +
            "PBuI2UPJAQCOltzV6Hx0Kt6PNZoaX/l3sg3MEr9cSvZerOw7uI8OAw==\n" +
            "=kBxO\n" +
            "-----END PGP PUBLIC KEY BLOCK-----\n";

        private const string PabloPrimaryFingerprint = "3EC28B27CD303B2BB2BD18E794297C45C682A409";
        private const string PabloEncryptionSubkeyFingerprint = "717704F949007BCC35A74DAE38D680EF7E55600C";

        [Fact]
        public void HasUsableEncryptionKey_KeyWithEncryptionSubkey_ReturnsTrue()
        {
            string armored = BuildRsaKey(withEncryptionSubkey: true);
            Assert.True(PgpCapabilities.HasUsableEncryptionKey(armored));
        }

        [Fact]
        public void HasUsableEncryptionKey_SignOnlyKey_ReturnsFalse()
        {
            // Certify/Sign-only primary, no encryption subkey — the case from the pablo.cash report.
            string armored = BuildRsaKey(withEncryptionSubkey: false);
            Assert.False(PgpCapabilities.HasUsableEncryptionKey(armored));
        }

        [Fact]
        public void HasUsableEncryptionKey_NullOrGarbage_ReturnsFalse()
        {
            Assert.False(PgpCapabilities.HasUsableEncryptionKey(null));
            Assert.False(PgpCapabilities.HasUsableEncryptionKey(string.Empty));
            Assert.False(PgpCapabilities.HasUsableEncryptionKey("not a pgp key"));
        }

        [Fact]
        public void GetPrimaryFingerprint_ReturnsMasterKeyFingerprint()
        {
            string armored = BuildRsaKey(withEncryptionSubkey: true, out string expectedPrimaryFp);
            var actual = PgpCapabilities.GetPrimaryFingerprint(armored);
            Assert.Equal(expectedPrimaryFp, actual);
        }

        [Fact]
        public void PabloUpdatedKey_IsUsableAndResolvesToPrimaryAndEncryptionSubkey()
        {
            Assert.True(PgpCapabilities.HasUsableEncryptionKey(PabloUpdatedKey));
            Assert.Equal(PabloPrimaryFingerprint, PgpCapabilities.GetPrimaryFingerprint(PabloUpdatedKey));

            var encKey = PgpCapabilities.SelectEncryptionKey(PabloUpdatedKey);
            Assert.NotNull(encKey);
            Assert.Equal(PabloEncryptionSubkeyFingerprint, ToHex(encKey!.GetFingerprint()));
        }

        private static string ToHex(byte[] data)
            => string.Concat(data.Select(b => b.ToString("X2")));

        private static string BuildRsaKey(bool withEncryptionSubkey)
            => BuildRsaKey(withEncryptionSubkey, out _);

        private static string BuildRsaKey(bool withEncryptionSubkey, out string primaryFingerprintHex)
        {
            var random = new SecureRandom();
            DateTime when = DateTime.UtcNow;

            var primaryFlags = new PgpSignatureSubpacketGenerator();
            primaryFlags.SetKeyFlags(false, KeyFlags.CertifyOther | KeyFlags.SignData);

            var ringGen = new PgpKeyRingGenerator(
                PgpSignature.PositiveCertification,
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), when),
                "Test RSA <test@example.com>",
                SymmetricKeyAlgorithmTag.Aes256,
                Array.Empty<char>(),
                true,
                primaryFlags.Generate(),
                null,
                random);

            if (withEncryptionSubkey)
            {
                var subkeyFlags = new PgpSignatureSubpacketGenerator();
                subkeyFlags.SetKeyFlags(false, KeyFlags.EncryptComms | KeyFlags.EncryptStorage);
                ringGen.AddSubKey(
                    new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), when),
                    subkeyFlags.Generate(),
                    null);
            }

            var pubRing = ringGen.GeneratePublicKeyRing();
            primaryFingerprintHex = ToHex(pubRing.GetPublicKey().GetFingerprint());

            using var ms = new MemoryStream();
            using (var aos = new ArmoredOutputStream(ms))
            {
                pubRing.Encode(aos);
            }

            return Encoding.ASCII.GetString(ms.ToArray());
        }

        private static AsymmetricCipherKeyPair GenerateRsa(SecureRandom random, int bits)
        {
            var gen = new RsaKeyPairGenerator();
            gen.Init(new RsaKeyGenerationParameters(
                BigInteger.ValueOf(0x10001), random, bits, 25));
            return gen.GenerateKeyPair();
        }
    }
}
