namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Every alias the public search knows, in one place. Each entry is backed by real searches
    /// from the site's search log (counts are for the 120 days to 2026-11-13); add new ones only
    /// when the log shows people using that wording. Never map unrelated things.
    /// </summary>
    public static class SearchSynonyms
    {
        /// <summary>
        /// Whole-query rewrites, applied after "no kyc" is taken out of the query. Keys and values
        /// are folded lower-case text (see <see cref="DirectorySearchEngine.Fold"/>).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> PhraseAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // "buy monero" 19, "buy crypto" 10, "buy xmr" 7, "on ramp" 3, "buy crypto no kyc" 3: every
            // word is a stop word, so these used to match random listings named "Buy …".
            ["buy monero"] = "exchange",
            ["buy xmr"] = "exchange",
            ["buy crypto"] = "exchange",
            ["buy cryptocurrency"] = "exchange",
            ["buy monero anonymously"] = "exchange",
            ["on ramp"] = "exchange",
            ["onramp"] = "exchange",

            // "credit card" 11 (ranked eSIM shops first), "debit card(s)" 7, "virtual card(s)" 11.
            ["credit card"] = "prepaid card",
            ["credit cards"] = "prepaid card",
            ["creditcard"] = "prepaid card",
            ["creditcards"] = "prepaid card",
            ["debit card"] = "prepaid card",
            ["debit cards"] = "prepaid card",
            ["debit"] = "prepaid card",

            // "artificial intelligence" 3, "inteligence artificial" 2, "artificial" 2.
            ["artificial intelligence"] = "ai",
            ["artificial inteligence"] = "ai",
            ["inteligence artificial"] = "ai",
            ["artificial"] = "ai",

            // "peer to peer" 2, "peer-to-peer crypto trading" 5 (the listings say "P2P" or "Peer-to-Peer").
            ["peer to peer"] = "p2p",

            // "virtual private network" (VPN spelled out).
            ["virtual private network"] = "vpn",
        };

        /// <summary>
        /// Same meaning, both ways. A listing found only through one of these ranks almost like a
        /// direct match (so a Gambling listing shows for "casino" ahead of a swap site whose
        /// description happens to mention a casino).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Equivalents = BuildEquivalents(
            new[] { "casino", "gambling", "betting" },  // gambling 107, casino 63, gamble 7, betting 3
            new[] { "porn", "adult", "xxx" },           // porn 103, adult 17, porno 4, xxx 2
            new[] { "bitcoin", "btc" },                 // bitcoin 23, btc 5
            new[] { "p2p", "peer" });                   // p2p 40, peer 14

        /// <summary>
        /// Related words (one way). A listing found only through one of these is shown after every
        /// direct match, so the word people typed still wins.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Related = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["sim"] = new[] { "esim", "phone" },    // sim 283
            ["esim"] = new[] { "sim" },             // esim 351
            ["phone"] = new[] { "sms", "sim" },     // phone 418
            ["sms"] = new[] { "phone", "verification" }, // sms 609
            ["otp"] = new[] { "sms", "verification" },   // otp 11 (one result before)
            ["mail"] = new[] { "email" },           // mail 192
            ["email"] = new[] { "mail" },           // email 333
            ["rdp"] = new[] { "vps" },              // rdp 51
            ["vps"] = new[] { "server" },           // vps 617
            ["server"] = new[] { "vps", "hosting" }, // server(s) 57
            ["hosting"] = new[] { "server", "vps" }, // hosting 85
            ["dex"] = new[] { "decentralized" },    // dex 75
            ["gamble"] = new[] { "gambling", "casino" }, // gamble 7
            ["bet"] = new[] { "gambling", "betting" },  // bet 7
            ["sex"] = new[] { "adult" },            // sex 11
            ["porno"] = new[] { "porn", "adult" },  // porno 4
            ["ai"] = new[] { "llm", "gpt" },        // ai 286
            ["llm"] = new[] { "ai" },               // llm 16
            ["chatgpt"] = new[] { "ai", "gpt" },    // chatgpt 15
            ["gift"] = new[] { "giftcard" },        // gift 52
            ["giftcard"] = new[] { "gift" },        // giftcard 12, giftcards 7
            ["vcc"] = new[] { "prepaid", "virtual" }, // vcc 17
        };

        private static Dictionary<string, string[]> BuildEquivalents(params string[][] groups)
        {
            var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                foreach (var word in group)
                {
                    map[word] = group.Where(w => w != word).ToArray();
                }
            }

            return map;
        }
    }
}
