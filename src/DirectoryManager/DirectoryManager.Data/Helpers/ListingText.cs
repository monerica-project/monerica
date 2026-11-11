namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// One set of length limits for listing text, used by the public submission form, the
    /// submission record, the admin edit form and the listing itself, so anything accepted at
    /// the start can always be approved at the end.
    /// Line breaks are stored as a single "\n": browsers post textarea line breaks as "\r\n",
    /// which used to make the same text 1 character longer per line on each round trip.
    /// </summary>
    public static class ListingText
    {
        public const int DescriptionMaxLength = 175;
        public const int NoteMaxLength = 250;
        public const int LinkMaxLength = 500;

        public static string? NormalizeLineBreaks(string? value) =>
            value?.Replace("\r\n", "\n").Replace('\r', '\n');

        /// <summary>
        /// Listings never shout: "Fast swaps!" → "Fast swaps." and "Great!!! Easy" → "Great. Easy".
        /// A "!" inside a word or URL (e.g. "Yahoo!Mail", "/#!/page") is left alone.
        /// </summary>
        public static string? RemoveExclamations(string? value)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains('!'))
            {
                return value;
            }

            var s = System.Text.RegularExpressions.Regex.Replace(value, @"[!¡]+(?=\s|$)", ".");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"([.?])\.+", "$1");
            return s.Trim();
        }

        /// <summary>
        /// Listing text (description, note) for display on Monerica: a listing's
        /// description and note get exclamation marks removed and British spellings
        /// changed to American ones.
        /// </summary>
        public static string? CleanListingText(string? value) => ToAmericanSpelling(RemoveExclamations(value));

        /// <summary>
        /// Monerica uses American spelling only: "itemised" → "itemized", "colour" → "color",
        /// "centre" → "center", "travelled" → "traveled". Only listed word families change (a
        /// blanket -ise rule would break "promise", "enterprise", "advertise"), the first letter's
        /// case is kept, and URLs and email addresses are never touched.
        /// </summary>
        public static string? ToAmericanSpelling(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return UrlOrEmailRx.Replace(value, "\u0000$0\u0000")
                .Split('\u0000')
                .Select((part, i) => i % 2 == 1 ? part : BritishWordRx.Replace(part, m => MatchCase(m.Value, American(m.Value))))
                .Aggregate(new System.Text.StringBuilder(), (sb, p) => sb.Append(p))
                .ToString();
        }

        // -ise / -isation families (stems), US: -ize / -ization.
        private static readonly string[] IseStems =
        {
            "item", "organ", "recogn", "priorit", "optim", "custom", "minim", "maxim", "special",
            "author", "util", "final", "personal", "monet", "anonym", "summar", "categor", "standard",
            "central", "decentral", "local", "synchron", "emphas", "apolog", "real", "critic", "visual",
            "character", "modern", "digit", "token", "capital", "global", "harmon", "legal", "neutral",
            "normal", "stabil", "subsid", "symbol", "steril", "commercial", "memor", "sanit", "random",
            "initial", "virtual", "mobil", "equal", "familiar", "general", "industrial", "ideal",
            "econom", "sympath", "privat", "jeopard", "fertil", "vandal", "util", "revolution",
            "decimal", "serial", "public", "lyophil", "pasteur", "homogen", "carbon", "vapor", "fossil", "crystal", "miniatur", "winter", "weather",
        };

        private static readonly string[] IseSuffixes =
        {
            "ise", "ised", "ises", "ising", "isation", "isations", "iser", "isers",
        };

        // Whole words, British → American. Lower case; case is restored on replace.
        private static readonly Dictionary<string, string> Words = BuildWords();

        private static readonly System.Text.RegularExpressions.Regex BritishWordRx = new System.Text.RegularExpressions.Regex(
            @"\b(?:" + string.Join("|", Words.Keys.OrderByDescending(k => k.Length).Select(System.Text.RegularExpressions.Regex.Escape)) + @")\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        private static readonly System.Text.RegularExpressions.Regex UrlOrEmailRx = new System.Text.RegularExpressions.Regex(
            @"(?:https?://|www\.)\S+|\S+@\S+\.\S+|\b[\w-]+(?:\.[\w-]+)*\.(?:com|net|org|io|xyz|onion|me|co|app|dev|to)(?:/\S*)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        private static Dictionary<string, string> BuildWords()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stem in IseStems.Distinct())
            {
                foreach (var suf in IseSuffixes)
                {
                    d[stem + suf] = stem + "iz" + suf[2..];
                }
            }

            // "analyse" family ("analyses" is left alone: it is also the plural of "analysis").
            foreach (var stem in new[] { "analy", "paraly", "cataly" })
            {
                foreach (var suf in new[] { "se", "sed", "sing", "ser", "sers" })
                {
                    d[stem + suf] = stem + "z" + suf[1..];
                }
            }

            // -our → -or.
            foreach (var stem in new[] { "col", "fav", "behavi", "hon", "lab", "neighb", "flav", "hum", "rum", "harb", "endeav", "sav", "vap", "arm", "od", "parl", "val", "vig", "splend" })
            {
                foreach (var suf in new[] { "our", "ours", "oured", "ouring", "ourful", "ourite", "ourites", "ourable", "ourably", "oural", "ourhood", "ourless", "oury", "ourer", "ourers" })
                {
                    d[stem + suf] = stem + "or" + suf[3..];
                }
            }

            // -re → -er.
            foreach (var stem in new[] { "cent", "met", "kilomet", "centimet", "millimet", "lit", "fib", "theat", "calib", "somb", "spect", "lust", "meag", "sab" })
            {
                d[stem + "re"] = stem + "er";
                d[stem + "res"] = stem + "ers";
            }

            d["centred"] = "centered";
            d["centring"] = "centering";

            // Doubled -l.
            foreach (var stem in new[] { "travel", "cancel", "label", "model", "fuel", "signal", "total", "level", "channel", "counsel", "marvel", "tunnel", "dial", "jewel", "rival" })
            {
                d[stem + "led"] = stem + "ed";
                d[stem + "ling"] = stem + "ing";
                d[stem + "ler"] = stem + "er";
                d[stem + "lers"] = stem + "ers";
            }

            foreach (var (gb, us) in new[]
            {
                ("counsellor", "counselor"), ("counsellors", "counselors"), ("marvellous", "marvelous"),
                ("jewellery", "jewelry"), ("enrol", "enroll"), ("enrols", "enrolls"), ("enrolment", "enrollment"),
                ("fulfil", "fulfill"), ("fulfils", "fulfills"), ("fulfilment", "fulfillment"),
                ("instalment", "installment"), ("instalments", "installments"), ("skilful", "skillful"),
                ("wilful", "willful"), ("defence", "defense"), ("defences", "defenses"), ("offence", "offense"),
                ("offences", "offenses"), ("licence", "license"), ("licences", "licenses"), ("licenced", "licensed"),
                ("pretence", "pretense"), ("catalogue", "catalog"), ("catalogues", "catalogs"), ("analogue", "analog"),
                ("programme", "program"), ("programmes", "programs"), ("grey", "gray"), ("greys", "grays"),
                ("mould", "mold"), ("moulds", "molds"), ("mouldy", "moldy"), ("aluminium", "aluminum"),
                ("tyre", "tire"), ("tyres", "tires"), ("ageing", "aging"), ("cosy", "cozy"), ("sceptic", "skeptic"),
                ("sceptical", "skeptical"), ("scepticism", "skepticism"), ("manoeuvre", "maneuver"),
                ("manoeuvres", "maneuvers"), ("whilst", "while"), ("amongst", "among"), ("learnt", "learned"),
                ("cheque", "check"), ("cheques", "checks"), ("pyjamas", "pajamas"), ("plough", "plow"),
                ("judgement", "judgment"), ("judgements", "judgments"), ("acknowledgement", "acknowledgment"),
                ("orientated", "oriented"), ("artefact", "artifact"), ("artefacts", "artifacts"),
                ("paediatric", "pediatric"), ("anaesthetic", "anesthetic"), ("encyclopaedia", "encyclopedia"),
                ("storeys", "stories"), ("draught", "draft"), ("kerb", "curb"), ("practise", "practice"),
                ("practised", "practiced"), ("practising", "practicing"), ("vapourise", "vaporize"),
            })
            {
                d[gb] = us;
            }

            return d;
        }

        private static string American(string word) => Words.TryGetValue(word, out var us) ? us : word;

        private static string MatchCase(string original, string replacement)
        {
            if (original.Length > 1 && original.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            {
                return replacement.ToUpperInvariant();
            }

            return char.IsUpper(original[0]) ? char.ToUpperInvariant(replacement[0]) + replacement[1..] : replacement;
        }
    }
}
