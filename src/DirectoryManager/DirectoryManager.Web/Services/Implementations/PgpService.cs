﻿using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Bcpg.Sig;

namespace DirectoryManager.Web.Services.Interfaces
{
    public class PgpService : IPgpService
    {
        public string? GetFingerprint(string armoredPublicKey)
        {
            try
            {
                // NOTE: this is the *identity* fingerprint that is persisted (author fingerprint,
                // raffle de-duplication, etc.). Its selection is intentionally left identical to the
                // historical behaviour so that previously stored fingerprints continue to match.
                var key = ReadIdentityKey(armoredPublicKey);
                if (key is null)
                {
                    return null;
                }

                byte[] fp = key.GetFingerprint();
                return string.Concat(fp.Select(b => b.ToString("X2")));
            }
            catch
            {
                return null;
            }
        }

        public string EncryptTo(string armoredPublicKey, string message)
        {
            // Encryption MUST target a key whose usage flags allow encryption. RSA primary keys are
            // commonly Certify/Sign-only with a separate Encrypt subkey; selecting the wrong key here
            // produces ciphertext that GnuPG (and other RFC 4880 compliant clients) refuse to decrypt
            // because the recipient has no *encryption* secret key for that key id.
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

        /// <summary>
        /// Selects the public key that messages should be encrypted to.
        /// Honors RFC 4880 key-usage flags: a key is only treated as an encryption recipient when its
        /// self-signature / subkey-binding signature carries EncryptComms or EncryptStorage. A valid,
        /// non-revoked, non-expired encryption subkey is always preferred over the primary key, and the
        /// newest such subkey wins when more than one exists.
        /// </summary>
        private static PgpPublicKey? ReadEncryptionKey(string armoredPublicKey)
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

            // Prefer a dedicated encryption subkey; fall back to an encryption-capable primary.
            return bestSubkey ?? bestPrimary;
        }

        /// <summary>
        /// Determines whether a key may be used as an encryption recipient.
        /// </summary>
        private static bool IsUsableForEncryption(PgpPublicKeyRing ring, PgpPublicKey key)
        {
            // The public-key algorithm must be able to encrypt at all. NOTE: PgpPublicKey.IsEncryptionKey
            // is algorithm-based only, so it returns true for an RSA primary key even when that key is
            // flagged Sign/Certify-only. That is exactly why the usage-flag check below is required.
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

            int? flags = GetKeyUsageFlags(ring, key);
            if (flags.HasValue)
            {
                return (flags.Value & (KeyFlags.EncryptComms | KeyFlags.EncryptStorage)) != 0;
            }

            // Older keys may carry no usage flags at all; fall back to algorithm capability.
            return true;
        }

        /// <summary>
        /// Reads the aggregated key-usage flags advertised by the primary key for this key
        /// (self-certification for the primary key, subkey-binding signature for subkeys).
        /// Returns null when the key advertises no usage flags.
        /// </summary>
        private static int? GetKeyUsageFlags(PgpPublicKeyRing ring, PgpPublicKey key)
        {
            long primaryKeyId = ring.GetPublicKey().KeyId;
            int? flags = null;

            foreach (PgpSignature sig in key.GetSignatures())
            {
                // Only trust signatures issued by the key's own primary key.
                if (sig.KeyId != primaryKeyId)
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

        /// <summary>
        /// Selects the key used to derive the persisted identity fingerprint.
        /// This deliberately mirrors the historical selection (first encryption-capable key, otherwise
        /// the first key) so that fingerprints already stored against reviews/raffle entries stay stable.
        /// </summary>
        private static PgpPublicKey? ReadIdentityKey(string armoredPublicKey)
        {
            using var keyIn = PgpUtilities.GetDecoderStream(
                new MemoryStream(Encoding.UTF8.GetBytes(armoredPublicKey)));

            var bundle = new PgpPublicKeyRingBundle(keyIn);

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
            {
                foreach (PgpPublicKey k in ring.GetPublicKeys())
                {
                    if (k.IsEncryptionKey)
                    {
                        return k;
                    }
                }
            }

            foreach (PgpPublicKeyRing ring in bundle.GetKeyRings())
            {
                foreach (PgpPublicKey k in ring.GetPublicKeys())
                {
                    return k;
                }
            }

            return null;
        }
    }
}