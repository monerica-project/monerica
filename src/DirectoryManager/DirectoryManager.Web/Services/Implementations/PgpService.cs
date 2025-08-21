using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

namespace DirectoryManager.Web.Services.Interfaces
{
    public class PgpService : IPgpService
    {
        public string? GetFingerprint(string armoredPublicKey)
        {
            try
            {
                var pubKey = ReadEncryptionKey(armoredPublicKey);
                if (pubKey is null)
                    return null;

                byte[] fp = pubKey.GetFingerprint();
                return string.Concat(fp.Select(b => b.ToString("X2"))); // uppercase hex
            }
            catch
            {
                return null;
            }
        }

        public string EncryptTo(string armoredPublicKey, string message)
        {
            var pubKey = ReadEncryptionKey(armoredPublicKey)
                         ?? throw new ArgumentException("No suitable encryption key found.", nameof(armoredPublicKey));

            var msgBytes = Encoding.UTF8.GetBytes(message);

            using var outMem = new MemoryStream();
            using var armoredOut = new ArmoredOutputStream(outMem);

            // Set up encrypted data generator (AES-256 + integrity packet)
            var encGen = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Aes256, true, new SecureRandom());
            encGen.AddMethod(pubKey);

            using (var encOut = encGen.Open(armoredOut, new byte[1 << 16]))
            {
                // Optional: compress literal data (ZIP)
                var compGen = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
                using (var compOut = compGen.Open(encOut))
                {
                    // Write literal data packet
                    var litGen = new PgpLiteralDataGenerator();
                    using var litOut = litGen.Open(
                        compOut,
                        PgpLiteralData.Binary,
                        "message.txt",
                        msgBytes.Length,
                        DateTime.UtcNow);

                    litOut.Write(msgBytes, 0, msgBytes.Length);
                }
            }

            armoredOut.Close();
            return Encoding.ASCII.GetString(outMem.ToArray());
        }

        private static PgpPublicKey? ReadEncryptionKey(string armoredPublicKey)
        {
            using var keyIn = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));

            var bundle = new PgpPublicKeyRingBundle(keyIn);

            // Prefer an explicit encryption-capable subkey
            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
                foreach (PgpPublicKey k in ring.GetPublicKeys())
                    if (k.IsEncryptionKey)
                        return k;

            // Fallback: return the first public key if none flagged as encryption key
            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
                foreach (PgpPublicKey k in ring.GetPublicKeys())
                    return k;

            return null;
        }
    }
}
