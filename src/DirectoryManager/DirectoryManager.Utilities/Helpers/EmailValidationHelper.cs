using System.Globalization;
using System.Net.Mail;

namespace DirectoryManager.Utilities.Helpers
{
    public static class EmailValidationHelper
    {
        /// <summary>
        /// Validates and normalizes an email.
        /// - Trims whitespace
        /// - IDN-converts the domain
        /// - Basic sanity checks + MailAddress parse
        /// Returns (IsValid, NormalizedEmail, ErrorMessage).
        /// </summary>
        /// <returns>The result.</returns>
        public static (bool IsValid, string? Email, string? Error) Validate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, null, "A valid email is required.");
            }

            var trimmed = input.Trim();

            // Quick rejects
            if (trimmed.Length > 254 || trimmed.Contains(' ') || trimmed.Contains("\r") || trimmed.Contains("\n"))
            {
                return (false, null, "Please enter a valid email address.");
            }

            // Split local@domain
            var at = trimmed.LastIndexOf('@');
            if (at <= 0 || at == trimmed.Length - 1)
            {
                return (false, null, "Please enter a valid email address.");
            }

            var local = trimmed.Substring(0, at);
            var domain = trimmed.Substring(at + 1);

            // Local part length per RFC guideline
            if (local.Length == 0 || local.Length > 64)
            {
                return (false, null, "Please enter a valid email address.");
            }

            // IDN-map the domain to ASCII (handles unicode domains safely)
            try
            {
                var idn = new IdnMapping();
                var asciiDomain = idn.GetAscii(domain);

                // Basic domain sanity: must have a dot and not start/end with '-'/'.'
                if (!asciiDomain.Contains('.') || asciiDomain.StartsWith("-") || asciiDomain.StartsWith(".") ||
                    asciiDomain.EndsWith("-") || asciiDomain.EndsWith(".") || asciiDomain.Contains(".."))
                {
                    return (false, null, "Please enter a valid email address.");
                }

                var normalized = $"{local}@{asciiDomain}".Trim();

                // Final parse check
                try
                {
                    _ = new MailAddress(normalized);
                }
                catch
                {
                    return (false, null, "Please enter a valid email address.");
                }

                return (true, normalized, null);
            }
            catch
            {
                return (false, null, "Please enter a valid email address.");
            }
        }
    }
}
