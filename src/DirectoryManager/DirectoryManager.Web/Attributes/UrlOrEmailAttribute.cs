using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Attributes
{
    /// <summary>
    /// Validates that an optional contact field (Messenger / Social / Email), when provided,
    /// is made up of proper URL(s) and/or email address(es) — never a bare handle like "@name".
    /// Multiple whitespace/comma-separated values are allowed; every token must qualify.
    /// Set <see cref="AllowUrl"/> = false to require email addresses only (the Email field).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class UrlOrEmailAttribute : ValidationAttribute
    {
        private static readonly Regex EmailRegex = new (
            @"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        // A scheme-less URL with a dotted host and optional path, e.g. t.me/foo, x.com/bar,
        // example.com. Accepted because it is unambiguously a link once https:// is prefixed.
        private static readonly Regex BareDomainRegex = new (
            @"^([A-Z0-9\-]+\.)+[A-Z]{2,}(/\S*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        /// <summary>When true (default) a URL is accepted; when false only email addresses are.</summary>
        public bool AllowUrl { get; set; } = true;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var text = value as string;

            // Optional field — use [Required] separately if it must be present.
            if (string.IsNullOrWhiteSpace(text))
            {
                return ValidationResult.Success;
            }

            var tokens = text.Split(
                new[] { ' ', '\t', '\r', '\n', ',' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in tokens)
            {
                var token = raw.Trim();
                if (token.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    token = token["mailto:".Length..];
                }

                if (EmailRegex.IsMatch(token))
                {
                    continue;
                }

                if (this.AllowUrl && IsUrl(token))
                {
                    continue;
                }

                var name = validationContext.DisplayName;
                var message = this.ErrorMessage ?? (this.AllowUrl
                    ? $"{name} must be a full link (e.g. https://t.me/yourname) or an email address — not a bare handle."
                    : $"{name} must be a valid email address.");
                return new ValidationResult(message, new[] { validationContext.MemberName! });
            }

            return ValidationResult.Success;
        }

        private static bool IsUrl(string token)
        {
            if (Uri.TryCreate(token, UriKind.Absolute, out var uri))
            {
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }

            return BareDomainRegex.IsMatch(token);
        }
    }
}
