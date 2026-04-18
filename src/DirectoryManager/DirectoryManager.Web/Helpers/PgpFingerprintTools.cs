// Web/Helpers/PgpFingerprintTools.cs
using System.Text;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DirectoryManager.Web.Helpers;

public static class PgpFingerprintTools
{
    /// <summary>
    /// Return ALL fingerprints found in the armored public key text (primary + subkeys),
    /// as uppercase hex. Also includes common short forms (last 32 and last 16 hex).
    /// </summary>
    /// <returns>A hashed string.</returns>
    public static HashSet<string> GetAllFingerprints(string? armored)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(armored))
        {
            return results;
        }

        // Try to parse as a bundle first
        if (!TryCollectFromBundle(armored, results))
        {
            // Fallback to scanning objects
            TryCollectFromFactory(armored, results);
        }

        // Add short forms for easier matching
        foreach (var full in results.ToArray())
        {
            if (full.Length >= 32)
            {
                results.Add(full[^32..]);
            }

            if (full.Length >= 16)
            {
                results.Add(full[^16..]);
            }
        }

        return results;
    }

    /// <summary>
    /// Back-compat convenience (returns the first fingerprint if available).
    /// </summary>
    /// <returns>The fingerprint.</returns>
    public static string? GetFingerprintFromArmored(string? armored)
        => GetAllFingerprints(armored).FirstOrDefault();

    /// <summary>Normalize any fingerprint string to uppercase hex (strip spaces, 0x, openpgp4fpr:).</summary>
    /// <returns>The normalized value.</returns>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return string.Empty;
        }

        s = s.Trim();

        if (s.StartsWith("openpgp4fpr:", StringComparison.OrdinalIgnoreCase))
        {
            s = s[12..];
        }

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
        }

        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if ((c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'F') ||
                (c >= 'a' && c <= 'f'))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
        }

        return sb.ToString();
    }

    /// <summary>Match supports exact or short-ID suffix matching.</summary>
    /// <returns>If there is a match.</returns>
    public static bool Matches(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        return a.EndsWith(b, StringComparison.Ordinal) || b.EndsWith(a, StringComparison.Ordinal);
    }

    private static bool TryCollectFromBundle(string armored, HashSet<string> results)
    {
        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(armored));
            using var decoded = PgpUtilities.GetDecoderStream(ms);
            var bundle = new PgpPublicKeyRingBundle(decoded);

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
            {
                CollectFromRing(ring, results);
            }

            return results.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCollectFromFactory(string armored, HashSet<string> results)
    {
        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(armored));
            using var decoded = PgpUtilities.GetDecoderStream(ms);
            var factory = new PgpObjectFactory(decoded);

            for (PgpObject? obj = factory.NextPgpObject(); obj is not null; obj = factory.NextPgpObject())
            {
                if (obj is PgpPublicKeyRing ring)
                {
                    CollectFromRing(ring, results);
                }
                else if (obj is PgpCompressedData comp)
                {
                    using var compStream = comp.GetDataStream();
                    var inner = new PgpObjectFactory(compStream);
                    for (PgpObject? innerObj = inner.NextPgpObject(); innerObj is not null; innerObj = inner.NextPgpObject())
                    {
                        if (innerObj is PgpPublicKeyRing innerRing)
                        {
                            CollectFromRing(innerRing, results);
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void CollectFromRing(PgpPublicKeyRing ring, HashSet<string> results)
    {
        foreach (PgpPublicKey key in ring.GetPublicKeys())
        {
            var fp = key.GetFingerprint();
            if (fp is null || fp.Length == 0)
            {
                continue;
            }

            results.Add(BytesToHex(fp));
        }
    }

    private static string BytesToHex(byte[] data)
    {
        var chars = new char[data.Length * 2];
        int i = 0;
        foreach (byte b in data)
        {
            int hi = (b >> 4) & 0xF;
            int lo = b & 0xF;
            chars[i++] = (char)(hi < 10 ? '0' + hi : 'A' + (hi - 10));
            chars[i++] = (char)(lo < 10 ? '0' + lo : 'A' + (lo - 10));
        }

        return new string(chars);
    }
}
