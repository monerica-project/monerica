using System.Text.RegularExpressions;

namespace DirectoryManager.Services.Implementations
{
    /// <summary>
    /// Removes per-recipient secrets from an email before its content is stored in the send log.
    /// Covers the links this code base puts in emails:
    /// <list type="bullet">
    /// <item>newsletter confirmation: <c>/newsletter/confirm?token=…</c></item>
    /// <item>one-click unsubscribe (List-Unsubscribe header): <c>/unsubscribe/one-click?email=…&amp;token=…</c></item>
    /// <item>any other secret-looking query value (token, code, key, secret, sig/signature, password,
    /// otp, auth, reset …) in plain or HTML-encoded (<c>&amp;amp;</c>) URLs</item>
    /// <item>BTCPay invoice/checkout links: <c>/i/&lt;invoiceId&gt;</c> and <c>invoiceId=</c> / <c>/invoice?id=</c></item>
    /// <item>the recipient's own address (plain and URL-encoded), replaced with <c>[recipient]</c></item>
    /// </list>
    /// Redacting also makes otherwise-identical per-recipient messages identical, so they are stored once.
    /// </summary>
    public static class EmailContentRedactor
    {
        public const string Redacted = "[redacted]";
        public const string RecipientPlaceholder = "[recipient]";

        // Most recipients for which the addresses themselves are replaced (bulk BCC sends never
        // contain per-recipient addresses in the body anyway).
        private const int MaxRecipientsToReplace = 50;

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        // name=value after ? & ; (the ; also catches "&amp;" in HTML). The value runs to the next
        // separator, quote, bracket or whitespace.
        private static readonly Regex SecretQueryParam = new (
            @"(?<=[?&;])(?<name>token|access_token|refresh_token|id_token|code|key|apikey|api_key|secret|sig|signature|password|pass|pwd|otp|auth|reset|resettoken|confirm|invoiceid)=(?<value>[^&;\s""'<>#\])]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Timeout);

        // BTCPay checkout links: https://host/i/<invoiceId> (also /invoice?id=<invoiceId>).
        private static readonly Regex BtcPayInvoicePath = new (
            @"(?<prefix>https?://[^\s""'<>/]+(?:/[^\s""'<>]*?)?/i/)(?<value>[A-Za-z0-9_-]{6,})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Timeout);

        private static readonly Regex InvoiceIdQuery = new (
            @"(?<prefix>/invoice\?(?:[^\s""'<>#]*?(?:&|&amp;))?id=)(?<value>[^&;\s""'<>#]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Timeout);

        /// <summary>Returns <paramref name="input"/> with the secrets listed on the class replaced.</summary>
        /// <returns>The redacted text (null stays null).</returns>
        public static string? Redact(string? input, IReadOnlyCollection<string>? recipients = null)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var s = input;
            try
            {
                s = SecretQueryParam.Replace(s, m => m.Groups["name"].Value + "=" + Redacted);
                s = BtcPayInvoicePath.Replace(s, m => m.Groups["prefix"].Value + Redacted);
                s = InvoiceIdQuery.Replace(s, m => m.Groups["prefix"].Value + Redacted);
            }
            catch (RegexMatchTimeoutException)
            {
                // Pathological input: never store something we could not check.
                return Redacted;
            }

            if (recipients != null && recipients.Count > 0 && recipients.Count <= MaxRecipientsToReplace)
            {
                foreach (var r in recipients)
                {
                    var address = r?.Trim();
                    if (string.IsNullOrEmpty(address))
                    {
                        continue;
                    }

                    s = s.Replace(Uri.EscapeDataString(address), RecipientPlaceholder, StringComparison.OrdinalIgnoreCase);
                    s = s.Replace(address, RecipientPlaceholder, StringComparison.OrdinalIgnoreCase);
                }
            }

            return s;
        }
    }
}
