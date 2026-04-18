// DirectoryManager.Utilities/Validation/MoneroAddressValidator.cs
using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Lightweight, format-level Monero address validator.
    /// - Mainnet prefixes: '4' (standard/integrated), '8' (subaddress)
    /// - Length: 95 (standard/subaddress) or 106 (integrated)
    /// - Alphabet: Base58 without 0,O,I,l
    /// </summary>
    public static class MoneroAddressValidator
    {
        // Base58 (Bitcoin/CryptoNote) without visually ambiguous chars
        private static readonly Regex Base58Regex =
            new Regex("^[1-9A-HJ-NP-Za-km-z]+$", RegexOptions.Compiled);

        /// <summary>
        /// Validates mainnet Monero address by format.
        /// </summary>
        /// <param name="address">Address string (no spaces).</param>
        /// <param name="allowIntegrated">Allow 106-char integrated addresses (default: true).</param>
        /// <param name="allowSubaddress">Allow '8' subaddresses (default: true).</param>
        public static bool IsValid(string? address, bool allowIntegrated = true, bool allowSubaddress = true)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            // Must not contain whitespace
            if (address.Any(char.IsWhiteSpace))
            {
                return false;
            }

            // Base58 charset only
            if (!Base58Regex.IsMatch(address))
            {
                return false;
            }

            // Mainnet prefixes
            char prefix = address[0];
            bool prefixOk =
                prefix == '4' || (allowSubaddress && prefix == '8');
            if (!prefixOk)
            {
                return false;
            }

            // Length check
            int len = address.Length;
            bool lenOk = len == 95 || (allowIntegrated && len == 106);
            if (!lenOk)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Placeholder for strict checksum validation (CryptoNote base58 + Keccak).
        /// Keeps the same signature so you can swap it in later.
        /// </summary>
        public static bool IsValidStrict(string? address, bool allowIntegrated = true, bool allowSubaddress = true)
        {
            // For now, just proxy to format-level validation.
            // TODO: implement CryptoNote base58 chunked decode and 4-byte Keccak checksum verification.
            return IsValid(address, allowIntegrated, allowSubaddress);
        }
    }
}
