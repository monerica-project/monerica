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

        private static readonly HashSet<string> PageWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "exchange", "exchanges", "orders", "checkout", "transaction", "transactions", "status",
            "tracking", "guarantee", "payment", "payments", "invoice", "invoices", "trades", "swapping",
            "convert", "converter", "details", "detail", "receipt", "confirm", "confirmation", "complete",
            "completed", "success", "history", "account", "dashboard", "support", "contact", "widget",
            "anonpay", "monero", "bitcoin", "litecoin", "ethereum", "tether", "amount", "ticker", "network",
            "address", "refund", "language", "locale", "referral", "affiliate", "partner", "default", "create",
            "search", "result", "results", "review", "reviews", "tracker", "trocador", "onion",
            "order", "swap", "swaps", "trade", "track", "home", "index", "html", "page", "pages", "send", "sell",
            "aspx", "main", "start", "step", "view", "show", "info", "pair", "from", "coin", "coins", "rate", "rates",
        };

        /// <summary>True unless the link is only the site's general page (nothing in it beyond ordinary page words).</summary>
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
                // The id itself can be anything (letters, digits or both, any length), so its shape
                // is never judged. A link is only turned away when it has nothing beyond ordinary
                // page words and language codes, which means it is the exchange's general page.
                var isLanguageCode = part.Length <= 3 && part.All(char.IsLetter);
                if (!isLanguageCode && !PageWords.Contains(part))
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
