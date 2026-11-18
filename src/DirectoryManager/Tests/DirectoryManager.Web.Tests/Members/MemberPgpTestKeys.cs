using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Services.Interfaces;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Bcpg.Sig;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace DirectoryManager.Web.Tests.Members
{
    /// <summary>
    /// Real OpenPGP keys (public armor + secret ring) for the member key-link tests, built the same
    /// way as in Services/PgpCapabilitiesTests and PgpServiceTests (2048-bit RSA keeps them fast).
    /// </summary>
    public static class MemberPgpTestKeys
    {
        public static TestKey Generate(string userId = "Member <m@example.com>")
        {
            var random = new SecureRandom();
            var when = DateTime.UtcNow.AddMinutes(-5);
            var primaryFlags = new PgpSignatureSubpacketGenerator();
            primaryFlags.SetKeyFlags(false, KeyFlags.CertifyOther | KeyFlags.SignData);

            // Sign-only primary + encryption subkey (the usual GnuPG shape).
            var ringGen = new PgpKeyRingGenerator(
                PgpSignature.PositiveCertification,
                new PgpKeyPair(PublicKeyAlgorithmTag.RsaSign, GenerateRsa(random), when),
                userId,
                SymmetricKeyAlgorithmTag.Aes256,
                Array.Empty<char>(),
                true,
                primaryFlags.Generate(),
                null,
                random);

            var encFlags = new PgpSignatureSubpacketGenerator();
            encFlags.SetKeyFlags(false, KeyFlags.EncryptComms | KeyFlags.EncryptStorage);
            ringGen.AddSubKey(new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random), when), encFlags.Generate(), null);

            var pub = ringGen.GeneratePublicKeyRing();
            return new TestKey(Armor(pub), ringGen.GenerateSecretKeyRing(), pub);
        }

        /// <summary>
        /// The forged ring from <c>ForgedRing_AttackerReBindsVictimSubkey_...</c>: the attacker's own
        /// primary SIGNS a binding over the victim's PUBLIC encryption subkey, then adds a newer subkey
        /// of their own. Returns the forged armor and the attacker's secret ring WITHOUT the victim's
        /// secret (the attacker never has it).
        /// </summary>
        public static (string ForgedArmor, PgpSecretKeyRing AttackerSecrets, PgpPublicKey VictimSubkey) ForgeRingAroundVictimSubkey()
        {
            var random = new SecureRandom();
            var old = DateTime.UtcNow.AddYears(-1);
            AsymmetricCipherKeyPair victimEnc = GenerateRsa(random);
            var attackerPrimary = new PgpKeyPair(PublicKeyAlgorithmTag.RsaSign, GenerateRsa(random), old);
            var attackerSubkey = new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, GenerateRsa(random), DateTime.UtcNow.AddMinutes(-5));

            var flags = new PgpSignatureSubpacketGenerator();
            flags.SetKeyFlags(false, KeyFlags.CertifyOther | KeyFlags.SignData);
            var enc = new PgpSignatureSubpacketGenerator();
            enc.SetKeyFlags(false, KeyFlags.EncryptComms | KeyFlags.EncryptStorage);

            var forgedGen = new PgpKeyRingGenerator(
                PgpSignature.PositiveCertification, attackerPrimary, "Attacker <a@example.com>", SymmetricKeyAlgorithmTag.Aes256,
                Array.Empty<char>(), true, flags.Generate(), null, random);
            var victimSubkeyPair = new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, victimEnc, old);
            forgedGen.AddSubKey(victimSubkeyPair, enc.Generate(), null);
            forgedGen.AddSubKey(attackerSubkey, enc.Generate(), null);

            // What the attacker can actually decrypt with: their own primary + their own subkey only.
            var attackerOnlyGen = new PgpKeyRingGenerator(
                PgpSignature.PositiveCertification, attackerPrimary, "Attacker <a@example.com>", SymmetricKeyAlgorithmTag.Aes256,
                Array.Empty<char>(), true, flags.Generate(), null, random);
            attackerOnlyGen.AddSubKey(attackerSubkey, enc.Generate(), null);

            return (Armor(forgedGen.GeneratePublicKeyRing()), attackerOnlyGen.GenerateSecretKeyRing(), victimSubkeyPair.PublicKey);
        }

        public static long RecipientKeyId(string armoredCipher)
        {
            using var din = PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.UTF8.GetBytes(armoredCipher)));
            var list = (PgpEncryptedDataList)new PgpObjectFactory(din).NextPgpObject();
            return list.GetEncryptedDataObjects().Cast<PgpPublicKeyEncryptedData>().Single().KeyId;
        }

        /// <summary>Decrypts the challenge; null when this secret ring holds no matching key.</summary>
        public static string? TryDecrypt(string armoredCipher, PgpSecretKeyRing secrets)
        {
            using var din = PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.UTF8.GetBytes(armoredCipher)));
            var list = (PgpEncryptedDataList)new PgpObjectFactory(din).NextPgpObject();
            foreach (PgpPublicKeyEncryptedData ed in list.GetEncryptedDataObjects().Cast<PgpPublicKeyEncryptedData>())
            {
                var sk = secrets.GetSecretKey(ed.KeyId);
                if (sk == null)
                {
                    continue;
                }

                using var clear = ed.GetDataStream(sk.ExtractPrivateKey(Array.Empty<char>()));
                return ReadLiteral(new PgpObjectFactory(clear));
            }

            return null;
        }

        public static string Hex(byte[] data) => string.Concat(data.Select(b => b.ToString("X2")));

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

        private static string Armor(PgpPublicKeyRing ring)
        {
            using var ms = new MemoryStream();
            using (var aos = new ArmoredOutputStream(ms))
            {
                ring.Encode(aos);
            }

            return Encoding.ASCII.GetString(ms.ToArray());
        }

        private static AsymmetricCipherKeyPair GenerateRsa(SecureRandom random)
        {
            var gen = new RsaKeyPairGenerator();
            gen.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), random, 2048, 25));
            return gen.GenerateKeyPair();
        }

        public sealed class TestKey
        {
            public TestKey(string armored, PgpSecretKeyRing secrets, PgpPublicKeyRing ring)
            {
                this.Armored = armored;
                this.Secrets = secrets;
                this.Ring = ring;
            }

            public string Armored { get; }

            public PgpSecretKeyRing Secrets { get; }

            public PgpPublicKeyRing Ring { get; }

            /// <summary>The identity the review flow stores for this key.</summary>
            public string Fingerprint => new PgpService().GetFingerprint(this.Armored)!;
        }

        /// <summary>Site config with a moderator (directory) PGP key.</summary>
        public sealed class SiteConfig : ICacheService
        {
            private readonly string? moderatorKey;

            public SiteConfig(string? moderatorKey = null) => this.moderatorKey = moderatorKey;

            public void ClearSnippetCache(SiteConfigSetting snippetType)
            {
            }

            public Task<string> GetSnippetAsync(SiteConfigSetting snippetType) => Task.FromResult(
                snippetType == SiteConfigSetting.PgpKey ? this.moderatorKey ?? string.Empty
                : snippetType == SiteConfigSetting.SiteName ? "Example Directory"
                : string.Empty);
        }
    }
}
