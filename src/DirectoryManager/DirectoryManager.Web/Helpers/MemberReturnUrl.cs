using DirectoryManager.Web.Constants;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Where a member may be sent after signing in or signing up: only back to a member page
    /// (write a review, reply, my reviews, account). Anything else — admin pages, other sites,
    /// protocol-relative or backslash tricks, encoded dots — is dropped, so a crafted returnUrl
    /// can't bounce a member onto an admin page or off the site.
    /// </summary>
    public static class MemberReturnUrl
    {
        private const int MaxLength = 300;

        private static readonly string[] AllowedPrefixes =
        {
            MemberAuth.WriteReviewPathPrefix,
            MemberAuth.ReplyPathPrefix,
        };

        private static readonly string[] AllowedExact =
        {
            MemberAuth.MyReviewsPath,
            MemberAuth.AccountPath,
        };

        public static string? Safe(string? returnUrl)
        {
            var value = (returnUrl ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > MaxLength || value[0] != '/')
            {
                return null;
            }

            // "//host" and "/\host" are protocol-relative; "%" could hide "..", "/" or "\".
            if (value.StartsWith("//", StringComparison.Ordinal)
                || value.Contains('\\')
                || value.Contains('%')
                || value.Contains("..", StringComparison.Ordinal)
                || value.Any(char.IsControl)
                || value.Any(char.IsWhiteSpace))
            {
                return null;
            }

            var path = value.Split('?', '#')[0];
            if (path.Length != value.Length)
            {
                return null; // member pages need no query string
            }

            var lower = path.ToLowerInvariant();
            if (AllowedExact.Contains(lower, StringComparer.Ordinal))
            {
                return lower;
            }

            foreach (var prefix in AllowedPrefixes)
            {
                if (lower.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var rest = lower[prefix.Length..];
                    if (rest.Length > 0 && rest.Length <= 120 && rest.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
                    {
                        return lower;
                    }
                }
            }

            return null;
        }
    }
}
