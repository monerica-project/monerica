using System.Security.Cryptography;
using System.Text;

namespace DirectoryManager.Services.Implementations
{
    /// <summary>
    /// Signs and checks the per-recipient token in the newsletter's one-click unsubscribe link
    /// (RFC 8058 List-Unsubscribe). The email jobs create the token and the web app checks it, and
    /// the two do not share ASP.NET data-protection keys, so this is a plain HMAC-SHA256 over the
    /// address. The HMAC key is derived from a secret both sides already have: the SendGrid API
    /// key (every sending app reads it from its "SendGrid" config section, all from the same
    /// deploy setting). The derivation uses a fixed label, so the API key itself is never what
    /// signs anything and the token reveals nothing about it.
    /// Rotating the SendGrid key makes links in older emails stop working; people with such a
    /// link can still use the manual unsubscribe form.
    /// </summary>
    public sealed class UnsubscribeTokenSigner
    {
        private const string KeyLabel = "DirectoryManager newsletter one-click unsubscribe v1";
        private const string MessagePrefix = "unsubscribe:v1:";
        private const int TokenBytes = 16;

        private readonly byte[]? key;

        public UnsubscribeTokenSigner(string? sharedSecret)
        {
            if (!string.IsNullOrWhiteSpace(sharedSecret))
            {
                this.key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(sharedSecret), Encoding.UTF8.GetBytes(KeyLabel));
            }
        }

        /// <summary>Gets a value indicating whether a secret was configured (no secret = no tokens, every check fails).</summary>
        public bool IsConfigured => this.key != null;

        /// <summary>
        /// The address is compared case-insensitively (trimmed, lower-cased), so the token matches
        /// however the mail client or link scanner re-cases the query string.
        /// </summary>
        public static string Normalize(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        public string? CreateToken(string email)
        {
            if (this.key == null || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return Convert.ToHexStringLower(this.Sign(email));
        }

        public bool IsValid(string? email, string? token)
        {
            if (this.key == null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var trimmed = token.Trim();
            if (trimmed.Length != TokenBytes * 2)
            {
                return false;
            }

            byte[] given;
            try
            {
                given = Convert.FromHexString(trimmed);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(given, this.Sign(email));
        }

        // A 128-bit tag, hex-encoded: short, and only [0-9a-f], so the query-string guard
        // (SuspiciousInputGuard) can never mistake it for an SQL comment like "--".
        private byte[] Sign(string email) =>
            HMACSHA256.HashData(this.key!, Encoding.UTF8.GetBytes(MessagePrefix + Normalize(email)))[..TokenBytes];
    }
}
