using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// A submission the owner starts from nothing but a link (admin portal, "Submit a link").
    /// The site's name, description, category and the rest are left for the automated review to
    /// draft; the owner then approves or edits the suggestion like any other submission.
    /// </summary>
    public static class LinkOnlySubmission
    {
        /// <summary>First words of the note to the admin on every link-only submission.</summary>
        public const string Marker = "Link-only submission from the admin portal.";

        public const string NoteText = Marker + " Only the link was given: the name, description, category, tags and other details are to be drafted from the site.";

        public static bool IsLinkOnly(Submission s) =>
            s.NoteToAdmin != null && s.NoteToAdmin.StartsWith(Marker, StringComparison.Ordinal);

        /// <summary>
        /// Checks a pasted address and splits it into the listing link and, when a deeper page was
        /// pasted, a proof page. "https://shop.example/pay/monero" becomes the site
        /// "https://shop.example/" with that page as proof; a plain site address has no proof page.
        /// </summary>
        /// <returns>False with a reason when the address is not a usable web link.</returns>
        public static bool TryParse(string? pasted, out string link, out string? proofLink, out string error)
        {
            link = string.Empty;
            proofLink = null;
            error = string.Empty;

            var text = (pasted ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                error = "Enter a link.";
                return false;
            }

            if (text.Any(char.IsWhiteSpace))
            {
                error = "A link cannot contain spaces.";
                return false;
            }

            if (!text.Contains("://", StringComparison.Ordinal))
            {
                text = "https://" + text;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !uri.Host.Contains('.', StringComparison.Ordinal)
                || text.Length > 500)
            {
                error = "That is not a web link. Use an address like https://example.com.";
                return false;
            }

            var root = uri.GetLeftPart(UriPartial.Authority) + "/";
            var isDeepPage = uri.AbsolutePath.Trim('/').Length > 0 || uri.Query.Length > 1;
            link = root;
            proofLink = isDeepPage ? uri.GetLeftPart(UriPartial.Query) : null;
            return true;
        }

        /// <summary>A stand-in name until the review suggests the real one: the host without "www.".</summary>
        public static string PlaceholderName(string link)
        {
            var host = Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.Host : link;
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        }
    }
}
