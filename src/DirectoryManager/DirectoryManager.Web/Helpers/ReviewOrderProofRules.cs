using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// What a moderator needs to confirm a swap order behind a review: the link to the reviewer's
    /// OWN order (with its order id), and the destination wallet address that was typed into that
    /// order. Reviewers kept pasting the exchange's general page and leaving the address out, so
    /// the order could not be opened; these rules say so on the form instead.
    /// </summary>
    public static partial class ReviewOrderProofRules
    {
        public const string GenericLinkMessage =
            "This looks like the exchange's general page, not your own order. Paste the full link to YOUR order, the one that includes its order ID (for example https://exchange.example/order/AB12-CD34).";

        public const string AddressMissingMessage =
            "Please paste the destination wallet address you entered for this order (the address where you received, or were due to receive, your coins). Without it a moderator cannot open and confirm the order. You can add other notes below it.";

        /// <summary>True when the link carries something that can identify one order (an id in the path, query or fragment).</summary>
        /// <returns>True for a link to a specific order.</returns>
        public static bool LooksLikeOwnOrderLink(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var rest = uri.AbsolutePath + " " + uri.Query + " " + uri.Fragment;
            foreach (var part in rest.Split(new[] { '/', '?', '&', '=', '#', ' ', '.', '_' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // An order id is a longer token with digits in it, or a long mixed token.
                if ((part.Length >= 6 && part.Any(char.IsDigit)) || part.Length >= 16)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the text contains something shaped like a wallet address.</summary>
        /// <returns>True when an address-like token is present.</returns>
        public static bool ContainsWalletAddress(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // Addresses are long unbroken runs of letters and digits (Bitcoin 26+, Monero 95, 0x + 40 ...).
            // Links are removed first so an order URL pasted here is not mistaken for an address.
            var withoutLinks = Links().Replace(text, " ");
            return AddressLike().IsMatch(withoutLinks);
        }

        [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 250)]
        private static partial Regex Links();

        [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Za-z0-9]{26,}(?![A-Za-z0-9])", RegexOptions.CultureInvariant, 250)]
        private static partial Regex AddressLike();
    }
}
