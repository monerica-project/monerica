using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Services.Implementations
{
    /// <summary>
    /// Builds the <see cref="EmailSendLogContent"/> row stored for a send: redacted (see
    /// <see cref="EmailContentRedactor"/>), hashed over the redacted untruncated content so identical
    /// messages share one row, and each body capped at <see cref="MaxStoredBodyChars"/>.
    /// Only message content goes in: never the API key or the raw request payload.
    /// </summary>
    public static class EmailContentSnapshot
    {
        /// <summary>512 K characters per stored body.</summary>
        public const int MaxStoredBodyChars = 512 * 1024;

        public static EmailSendLogContent Build(
            string fromEmail,
            string? fromName,
            string? replyTo,
            IEnumerable<KeyValuePair<string, string>>? headers,
            string? plainText,
            string? html,
            IReadOnlyCollection<string> recipients)
        {
            var headerText = headers == null
                ? null
                : string.Join(
                    "\n",
                    headers
                        .Where(h => !string.IsNullOrWhiteSpace(h.Key))
                        .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(h => $"{h.Key}: {h.Value}"));
            if (string.IsNullOrEmpty(headerText))
            {
                headerText = null;
            }

            var redHeaders = EmailContentRedactor.Redact(headerText, recipients);
            var redText = EmailContentRedactor.Redact(plainText, recipients);
            var redHtml = EmailContentRedactor.Redact(html, recipients);
            var redReplyTo = string.IsNullOrWhiteSpace(replyTo) ? null : replyTo.Trim();

            var size = (long)Encoding.UTF8.GetByteCount(plainText ?? string.Empty)
                     + Encoding.UTF8.GetByteCount(html ?? string.Empty);

            return new EmailSendLogContent
            {
                ContentHash = Hash(fromEmail, fromName, redReplyTo, redHeaders, redText, redHtml),
                FromEmail = Clip(fromEmail ?? string.Empty, 320),
                FromName = string.IsNullOrEmpty(fromName) ? null : Clip(fromName, 200),
                ReplyTo = redReplyTo == null ? null : Clip(redReplyTo, 320),
                Headers = redHeaders,
                BodyText = Cap(redText, out var textCut),
                BodyTextTruncated = textCut,
                BodyHtml = Cap(redHtml, out var htmlCut),
                BodyHtmlTruncated = htmlCut,
                SizeBytes = (int)Math.Min(int.MaxValue, size),
                CreateDate = DateTime.UtcNow,
                LastUsedUtc = DateTime.UtcNow,
            };
        }

        /// <summary>Lower-case hex SHA-256 over the parts, each length-prefixed so the boundaries are unambiguous.</summary>
        /// <returns>The hash.</returns>
        public static string Hash(params string?[] parts)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (p == null)
                {
                    sb.Append("-1:");
                }
                else
                {
                    sb.Append(p.Length).Append(':').Append(p);
                }

                sb.Append('|');
            }

            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        private static string? Cap(string? s, out bool truncated)
        {
            truncated = s != null && s.Length > MaxStoredBodyChars;
            if (!truncated)
            {
                return s;
            }

            var cut = MaxStoredBodyChars;

            // Don't split a surrogate pair.
            if (char.IsHighSurrogate(s![cut - 1]))
            {
                cut--;
            }

            return s.Substring(0, cut);
        }

        private static string Clip(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}
