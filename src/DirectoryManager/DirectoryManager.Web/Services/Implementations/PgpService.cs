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
                var key = ReadEncryptionKey(armoredPublicKey);
                if (key is null) return null;

                byte[] fp = key.GetFingerprint();
                return string.Concat(fp.Select(b => b.ToString("X2")));
            }
            catch { return null; }
        }

        public string EncryptTo(string armoredPublicKey, string message)
        {
            var encKey = ReadEncryptionKey(armoredPublicKey)
                ?? throw new InvalidOperationException("No encryption-capable public key found in the provided key block.");

            byte[] data = Encoding.UTF8.GetBytes(message);

            using var outMs = new MemoryStream();
            using (var armored = new ArmoredOutputStream(outMs))
            {
                // ✨ remove armor headers so no “Version: …” line is emitted
                SuppressArmorHeaders(armored);

                var encGen = new PgpEncryptedDataGenerator(
                    SymmetricKeyAlgorithmTag.Aes256,
                    true); // with integrity packet

                encGen.AddMethod(encKey);

                using (Stream encOut = encGen.Open(armored, new byte[1 << 16]))
                {
                    var compGen = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);

                    using (Stream compOut = compGen.Open(encOut))
                    {
                        var litGen = new PgpLiteralDataGenerator();

                        using (Stream litOut = litGen.Open(
                            compOut,
                            PgpLiteralData.Text,  // or PgpLiteralData.Binary
                            "data",               // arbitrary name
                            data.Length,
                            DateTime.UtcNow))
                        {
                            litOut.Write(data, 0, data.Length);
                        }
                    }
                }
            }

            return Encoding.ASCII.GetString(outMs.ToArray());
        }

        private static void SuppressArmorHeaders(ArmoredOutputStream aos)
        {
            // Setting value to null removes the header
            aos.SetHeader("Version", null);
            aos.SetHeader("Comment", null);
            aos.SetHeader("MessageID", null);
            aos.SetHeader("Hash", null);
            aos.SetHeader("Charset", null);
        }

        private static PgpPublicKey? ReadEncryptionKey(string armoredPublicKey)
        {
            using var keyIn = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));

            var bundle = new PgpPublicKeyRingBundle(keyIn);

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
                foreach (PgpPublicKey k in ring.GetPublicKeys())
                    if (k.IsEncryptionKey)
                        return k;

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
                foreach (PgpPublicKey k in ring.GetPublicKeys())
                    return k;

            return null;
        }
    }
}
