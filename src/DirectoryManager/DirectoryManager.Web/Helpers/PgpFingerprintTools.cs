using System.Text;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DirectoryManager.Web.Helpers;

public static class PgpFingerprintTools
{
    public static string? GetFingerprintFromArmored(string armoredPublicKey)
    {
        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey));
            using var decoder = PgpUtilities.GetDecoderStream(ms);

            // Bundle works with BouncyCastle .NET (2.x)
            var bundle = new PgpPublicKeyRingBundle(decoder);

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
            {
                // Primary key is the first key in the ring
                var primary = ring.GetPublicKey();
                if (primary is null)
                {
                    continue;
                }

                var fp = primary.GetFingerprint(); // byte[]
                return BitConverter.ToString(fp).Replace("-", ""); // UPPERCASE hex
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}