// Web/Helpers/PgpCapabilities.cs
using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Bcpg.Sig;

namespace DirectoryManager.Web.Helpers;

/// <summary>
/// Capability inspection for OpenPGP public keys, shared by the ownership/review challenge
/// (which encrypts a one-time code to the key) and by upload-time validation.
///
/// SECURITY NOTE: ownership login matches a submitted key against a listing on the *primary*
/// fingerprint so that routine key updates (adding/rotating an encryption subkey, extending
/// expiry) keep working. That is only safe because the primary fingerprint is unforgeable — an
/// attacker would have to reuse the victim's real primary public key, whose secret they do not
/// hold. The one remaining way to hijack the challenge would be to append an attacker-controlled
/// encryption subkey and have the code encrypted to it; that is why <see cref="SelectEncryptionKey"/>
/// only accepts a subkey whose subkey-binding signature actually VERIFIES against the primary.
/// (Encrypting to the primary itself needs no such check: the attacker cannot decrypt with it.)
/// </summary>
public static class PgpCapabilities
{
    /// <summary>
    /// True when the armored public key has a usable encryption key (a verified encryption subkey,
    /// or an encryption-capable primary). Used to reject/warn on sign-only keys at upload time.
    /// </summary>
    /// <returns>Whether the key can be encrypted to.</returns>
    public static bool HasUsableEncryptionKey(string? armoredPublicKey)
    {
        if (string.IsNullOrWhiteSpace(armoredPublicKey))
        {
            return false;
        }

        try
        {
            return SelectEncryptionKey(armoredPublicKey) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Selects the public key that a message should be encrypted to. A valid, non-revoked,
    /// non-expired encryption subkey (with a cryptographically valid subkey-binding signature)
    /// is preferred, newest first; otherwise an encryption-capable primary is used.
    /// </summary>
    /// <returns>The chosen encryption key, or null when none is usable.</returns>
    public static PgpPublicKey? SelectEncryptionKey(string armoredPublicKey)
    {
        using var keyIn = PgpUtilities.GetDecoderStream(
            new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));

        var bundle = new PgpPublicKeyRingBundle(keyIn);

        PgpPublicKey? bestSubkey = null;
        PgpPublicKey? bestPrimary = null;

        foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
        {
            foreach (PgpPublicKey k in ring.GetPublicKeys())
            {
                if (!IsUsableForEncryption(ring, k))
                {
                    continue;
                }

                if (k.IsMasterKey)
                {
                    if (bestPrimary is null || k.CreationTime > bestPrimary.CreationTime)
                    {
                        bestPrimary = k;
                    }
                }
                else
                {
                    if (bestSubkey is null || k.CreationTime > bestSubkey.CreationTime)
                    {
                        bestSubkey = k;
                    }
                }
            }
        }

        return bestSubkey ?? bestPrimary;
    }

    /// <summary>
    /// Returns the uppercase-hex fingerprint of the PRIMARY (master) key, or null.
    /// Ownership matching compares this so that supersets of the same key (added subkeys,
    /// rotated subkeys, extended expiry) all resolve to the same owner.
    /// </summary>
    /// <returns>The primary key fingerprint, or null.</returns>
    public static string? GetPrimaryFingerprint(string? armoredPublicKey)
    {
        if (string.IsNullOrWhiteSpace(armoredPublicKey))
        {
            return null;
        }

        try
        {
            using var keyIn = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));

            var bundle = new PgpPublicKeyRingBundle(keyIn);

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
            {
                var master = ring.GetPublicKey(); // the master (primary) key of the ring
                byte[] fp = master.GetFingerprint();
                if (fp is { Length: > 0 })
                {
                    return BytesToHex(fp);
                }
            }
        }
        catch
        {
            // fall through to null
        }

        return null;
    }

    private static bool IsUsableForEncryption(PgpPublicKeyRing ring, PgpPublicKey key)
    {
        // Algorithm must be able to encrypt at all. IsEncryptionKey is algorithm-based only, so it is
        // true for an RSA primary even when that key is flagged sign/certify-only — hence the extra
        // checks below.
        if (!key.IsEncryptionKey)
        {
            return false;
        }

        if (key.IsRevoked())
        {
            return false;
        }

        long validSeconds = key.GetValidSeconds();
        if (validSeconds > 0 && key.CreationTime.AddSeconds(validSeconds) < DateTime.UtcNow)
        {
            return false;
        }

        var primary = ring.GetPublicKey();

        if (!key.IsMasterKey)
        {
            // Subkey: require a cryptographically VALID subkey-binding signature from the primary.
            // This is the security-critical check for the primary-fingerprint match path.
            return HasValidEncryptionBinding(primary, key);
        }

        // Primary used directly for encryption (rare). No signature verification is required for
        // safety here: an attacker cannot decrypt with the victim's primary key. Honor advertised
        // usage flags when present, otherwise fall back to algorithm capability (legacy keys).
        int? flags = GetAdvertisedKeyUsageFlags(primary, key);
        if (flags.HasValue)
        {
            return (flags.Value & (KeyFlags.EncryptComms | KeyFlags.EncryptStorage)) != 0;
        }

        return true;
    }

    /// <summary>
    /// True when the subkey carries a subkey-binding signature (0x18) issued by the primary that
    /// (a) verifies cryptographically and (b) either advertises an encryption usage flag or, for
    /// older keys, advertises no usage flags at all (algorithm capability then applies).
    /// </summary>
    private static bool HasValidEncryptionBinding(PgpPublicKey primary, PgpPublicKey subkey)
    {
        foreach (PgpSignature sig in subkey.GetSignatures())
        {
            if (sig.KeyId != primary.KeyId)
            {
                continue;
            }

            if (sig.SignatureType != PgpSignature.SubkeyBinding)
            {
                continue;
            }

            // If usage flags are present they must allow encryption; keys without flags fall back to
            // algorithm capability (already verified by IsEncryptionKey on the caller).
            PgpSignatureSubpacketVector? hashed = sig.GetHashedSubPackets();
            if (hashed is not null && hashed.HasSubpacket(SignatureSubpacketTag.KeyFlags))
            {
                int f = hashed.GetKeyFlags();
                if ((f & (KeyFlags.EncryptComms | KeyFlags.EncryptStorage)) == 0)
                {
                    continue;
                }
            }

            try
            {
                sig.InitVerify(primary);
                if (sig.VerifyCertification(primary, subkey))
                {
                    return true;
                }
            }
            catch
            {
                // Malformed/undecodable signature — treat as not binding and try the next one.
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the aggregated key-usage flags a signature (from the primary) advertises for the key.
    /// Not signature-verified: used only for the primary-key encryption path, which is safe without
    /// verification (see class remarks). Returns null when no usage flags are advertised.
    /// </summary>
    private static int? GetAdvertisedKeyUsageFlags(PgpPublicKey primary, PgpPublicKey key)
    {
        int? flags = null;

        foreach (PgpSignature sig in key.GetSignatures())
        {
            if (sig.KeyId != primary.KeyId)
            {
                continue;
            }

            PgpSignatureSubpacketVector? hashed = sig.GetHashedSubPackets();
            if (hashed is null || !hashed.HasSubpacket(SignatureSubpacketTag.KeyFlags))
            {
                continue;
            }

            flags = (flags ?? 0) | hashed.GetKeyFlags();
        }

        return flags;
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
