using System.Text;
using DirectoryManager.Web.Services.Interfaces;
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
    /// Regression tests for the RSA encryption-key selection bug.
    /// A GnuPG-style RSA key has a Certify/Sign-only primary key plus a separate Encrypt subkey.
    /// The service must encrypt to the Encrypt subkey, never to the sign-only primary, otherwise
    /// the recipient's GnuPG reports it has no secret key able to decrypt the message.
    /// (The bug is about key usage flags / key structure and is independent of RSA modulus size,
    /// so 2048-bit keys are used here to keep the test fast; 4096 behaves identically.)
    /// </summary>
    public class PgpServiceTests
    {
        private const string Message = "Monerica verification code: ABCD-1234";

        [Fact]
        public void EncryptTo_RsaSignOnlyPrimaryWithEncryptSubkey_TargetsEncryptionSubkeyNotPrimary()
        {
            var key = BuildRsaKey(out long primaryKeyId, out long encryptSubkeyId);
            var service = new PgpService();

            string armoredCipher = service.EncryptTo(key.ArmoredPublicKey, Message);

            long recipientKeyId = GetRecipientKeyId(armoredCipher);

            Assert.Equal(encryptSubkeyId, recipientKeyId);
            Assert.NotEqual(primaryKeyId, recipientKeyId);
        }

        [Fact]
        public void EncryptTo_RsaSignOnlyPrimaryWithEncryptSubkey_RoundTripsWithSubkeySecret()
        {
            var key = BuildRsaKey(out _, out _);
            var service = new PgpService();

            string armoredCipher = service.EncryptTo(key.ArmoredPublicKey, Message);

            string decrypted = Decrypt(armoredCipher, key.SecretRing);

            Assert.Equal(Message, decrypted);
        }

        [Fact]
        public void EncryptTo_PrefersNewestEncryptionSubkey()
        {
            var key = BuildRsaKeyWithTwoEncryptionSubkeys(out long newestSubkeyId);
            var service = new PgpService();

            string armoredCipher = service.EncryptTo(key.ArmoredPublicKey, Message);

            Assert.Equal(newestSubkeyId, GetRecipientKeyId(armoredCipher));
            Assert.Equal(Message, Decrypt(armoredCipher, key.SecretRing));
        }

        private static long GetRecipientKeyId(string armoredCipher)
        {
            using var din = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredCipher)));
            var encList = (PgpEncryptedDataList)new PgpObjectFactory(din).NextPgpObject();
            var ed = encList.GetEncryptedDataObjects()
                .Cast<PgpPublicKeyEncryptedData>()
                .Single();
            return ed.KeyId;
        }

        private static string Decrypt(string armoredCipher, PgpSecretKeyRing secretRing)
        {
            using var din = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredCipher)));
            var factory = new PgpObjectFactory(din);

            PgpObject obj = factory.NextPgpObject();
            if (obj is PgpEncryptedDataList encList)
            {
                foreach (PgpPublicKeyEncryptedData ed in encList.GetEncryptedDataObjects()
                             .Cast<PgpPublicKeyEncryptedData>())
                {
                    PgpSecretKey? sk = secretRing.GetSecretKey(ed.KeyId);
                    if (sk is null)
                    {
                        continue;
                    }

                    PgpPrivateKey priv = sk.ExtractPrivateKey(System.Array.Empty<char>());
                    using var clear = ed.GetDataStream(priv);
                    return ReadLiteral(new PgpObjectFactory(clear));
                }
            }

            throw new Xunit.Sdk.XunitException("Recipient secret key could not decrypt the ciphertext.");
        }

        private static string ReadLiteral(PgpObjectFactory factory)
        {
            for (PgpObject? o = factory.NextPgpObject(); o is not null; o = factory.NextPgpObject())
            {
                switch (o)
                {
                    case PgpCompressedData comp:
                        return ReadLiteral(new PgpObjectFactory(comp.GetDataStream()));
                    case PgpLiteralData lit:
                        using (var reader = new StreamReader(lit.GetInputStream(), Encoding.UTF8))
                        {
                            return reader.ReadToEnd();
                        }
                }
            }

            throw new Xunit.Sdk.XunitException("No literal data packet found.");
        }

        private static AsymmetricCipherKeyPair GenerateRsa(SecureRandom random, int bits)
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new RsaKeyGenerationParameters(
                BigInteger.ValueOf(0x10001), random, bits, 25));
            return generator.GenerateKeyPair();
        }

        private static GeneratedKey BuildRsaKey(out long primaryKeyId, out long encryptSubkeyId)
        {
            var random = new SecureRandom();
            DateTime when = DateTime.UtcNow;

            var primaryFlags = new PgpSignatureSubpacketGenerator();
            primaryFlags.SetKeyFlags(false, KeyFlags.CertifyOther | KeyFlags.SignData);

            var ringGen = new PgpKeyRingGenerator(
                PgpSignature.PositiveCertification,
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), when),
                "Ryan RSA <ryan@example.com>",
                SymmetricKeyAlgorithmTag.Aes256,
                System.Array.Empty<char>(),
                true,
                primaryFlags.Generate(),
                null,
                random);

            var subkeyFlags = new PgpSignatureSubpacketGenerator();
            subkeyFlags.SetKeyFlags(false, KeyFlags.EncryptComms | KeyFlags.EncryptStorage);
            ringGen.AddSubKey(
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), when),
                subkeyFlags.Generate(),
                null);

            var pubRing = ringGen.GeneratePublicKeyRing();
            var secRing = ringGen.GenerateSecretKeyRing();

            primaryKeyId = pubRing.GetPublicKey().KeyId;
            encryptSubkeyId = pubRing.GetPublicKeys()
                .Cast<PgpPublicKey>()
                .First(k => !k.IsMasterKey)
                .KeyId;

            return new GeneratedKey(Armor(pubRing), secRing);
        }

        private static GeneratedKey BuildRsaKeyWithTwoEncryptionSubkeys(out long newestSubkeyId)
        {
            var random = new SecureRandom();
            DateTime older = DateTime.UtcNow.AddDays(-30);
            DateTime newer = DateTime.UtcNow.AddDays(-1);

            var primaryFlags = new PgpSignatureSubpacketGenerator();
            primaryFlags.SetKeyFlags(false, KeyFlags.CertifyOther | KeyFlags.SignData);

            var ringGen = new PgpKeyRingGenerator(
                PgpSignature.PositiveCertification,
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), older),
                "Ryan RSA <ryan@example.com>",
                SymmetricKeyAlgorithmTag.Aes256,
                System.Array.Empty<char>(),
                true,
                primaryFlags.Generate(),
                null,
                random);

            var encFlags = new PgpSignatureSubpacketGenerator();
            encFlags.SetKeyFlags(false, KeyFlags.EncryptComms | KeyFlags.EncryptStorage);

            ringGen.AddSubKey(
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), older),
                encFlags.Generate(),
                null);
            ringGen.AddSubKey(
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random, 2048), newer),
                encFlags.Generate(),
                null);

            var pubRing = ringGen.GeneratePublicKeyRing();
            var secRing = ringGen.GenerateSecretKeyRing();

            newestSubkeyId = pubRing.GetPublicKeys()
                .Cast<PgpPublicKey>()
                .Where(k => !k.IsMasterKey)
                .OrderByDescending(k => k.CreationTime)
                .First()
                .KeyId;

            return new GeneratedKey(Armor(pubRing), secRing);
        }

        private static string Armor(PgpPublicKeyRing pubRing)
        {
            using var ms = new MemoryStream();
            using (var aos = new ArmoredOutputStream(ms))
            {
                pubRing.Encode(aos);
            }

            return Encoding.ASCII.GetString(ms.ToArray());
        }

        private sealed class GeneratedKey
        {
            public GeneratedKey(string armoredPublicKey, PgpSecretKeyRing secretRing)
            {
                this.ArmoredPublicKey = armoredPublicKey;
                this.SecretRing = secretRing;
            }

            public string ArmoredPublicKey { get; }

            public PgpSecretKeyRing SecretRing { get; }
        }
    }
}
