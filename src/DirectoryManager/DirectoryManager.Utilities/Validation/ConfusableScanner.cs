using System.Globalization;
using System.Text;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Detects homoglyph / look-alike spoofing in display text (e.g. a listing named with
    /// Cyrillic letters that render identically to Latin: "Cаkе" vs "Cake").
    ///
    /// This does NOT block anything. Its job is to (a) raise a flag for a human moderator,
    /// and (b) produce a "skeleton" so two visually-identical names can be compared for
    /// impersonation even when their code points differ.
    ///
    /// Coverage note: the confusable map below targets the Latin/Cyrillic/Greek look-alikes
    /// that account for essentially all real-world directory spoofing. Full Unicode coverage
    /// would load the official Unicode "confusables.txt" data file; this is a focused subset.
    /// </summary>
    public static class ConfusableScanner
    {
        public enum ScriptClass
        {
            Latin,
            Cyrillic,
            Greek,
            Other,
        }

        public sealed class ScanResult
        {
            public bool MixedConfusableScript { get; init; }

            public IReadOnlySet<ScriptClass> Scripts { get; init; } = new HashSet<ScriptClass>();

            public string Skeleton { get; init; } = string.Empty;
        }

        public static ScanResult Scan(string? input)
        {
            var scripts = new HashSet<ScriptClass>();

            if (string.IsNullOrEmpty(input))
            {
                return new ScanResult { Scripts = scripts, Skeleton = string.Empty };
            }

            foreach (var rune in input.EnumerateRunes())
            {
                var script = Classify(rune.Value);
                if (script != ScriptClass.Other)
                {
                    scripts.Add(script);
                }
            }

            bool hasLatin = scripts.Contains(ScriptClass.Latin);
            bool hasLookalike = scripts.Contains(ScriptClass.Cyrillic) || scripts.Contains(ScriptClass.Greek);

            return new ScanResult
            {
                Scripts = scripts,

                // The classic spoof signal: Latin mixed with Cyrillic/Greek in the same
                // token. (Latin+Han, Latin+Arabic etc. are common and legitimate, so they
                // are not flagged here.)
                MixedConfusableScript = hasLatin && hasLookalike,
                Skeleton = BuildSkeleton(input),
            };
        }

        /// <summary>
        /// Canonical comparison key: confusables folded to Latin, lowercased, whitespace
        /// removed. Two names with the same skeleton render alike and should be treated as
        /// potential impersonation of each other.
        /// </summary>
        public static string BuildSkeleton(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);

            foreach (var rune in input.Normalize(NormalizationForm.FormKD).EnumerateRunes())
            {
                if (ConfusableMap.TryGetValue(rune.Value, out var mapped))
                {
                    sb.Append(mapped);
                    continue;
                }

                var cat = Rune.GetUnicodeCategory(rune);
                if (cat is UnicodeCategory.SpaceSeparator
                        or UnicodeCategory.Control
                        or UnicodeCategory.Format)
                {
                    continue;
                }

                sb.Append(rune.ToString());
            }

            return sb.ToString().ToLowerInvariant();
        }

        private static ScriptClass Classify(int cp)
        {
            // Latin (Basic + Latin-1 letters + Latin Extended-A/B)
            if ((cp >= 0x0041 && cp <= 0x005A) ||
                (cp >= 0x0061 && cp <= 0x007A) ||
                (cp >= 0x00C0 && cp <= 0x024F))
            {
                return ScriptClass.Latin;
            }

            // Greek and Coptic
            if (cp >= 0x0370 && cp <= 0x03FF)
            {
                return ScriptClass.Greek;
            }

            // Cyrillic + Cyrillic Supplement
            if ((cp >= 0x0400 && cp <= 0x04FF) || (cp >= 0x0500 && cp <= 0x052F))
            {
                return ScriptClass.Cyrillic;
            }

            return ScriptClass.Other;
        }

        // Focused confusable -> ASCII map (Cyrillic & Greek look-alikes of Latin letters,
        // plus a few common digit/symbol confusables). Keyed by code point.
        private static readonly Dictionary<int, string> ConfusableMap = new ()
        {
            // Cyrillic lowercase that look like Latin
            { 0x0430, "a" }, // а
            { 0x0435, "e" }, // е
            { 0x043E, "o" }, // о
            { 0x0440, "p" }, // р
            { 0x0441, "c" }, // с
            { 0x0443, "y" }, // у
            { 0x0445, "x" }, // х
            { 0x0456, "i" }, // і
            { 0x0458, "j" }, // ј
            { 0x04BB, "h" }, // һ
            { 0x051B, "q" }, // ԛ
            { 0x0501, "d" }, // ԁ
            { 0x0261, "g" }, // ɡ (Latin small script g)

            // Cyrillic uppercase that look like Latin
            { 0x0410, "a" }, // А
            { 0x0412, "b" }, // В
            { 0x0415, "e" }, // Е
            { 0x041A, "k" }, // К
            { 0x041C, "m" }, // М
            { 0x041D, "h" }, // Н
            { 0x041E, "o" }, // О
            { 0x0420, "p" }, // Р
            { 0x0421, "c" }, // С
            { 0x0422, "t" }, // Т
            { 0x0423, "y" }, // У
            { 0x0425, "x" }, // Х
            { 0x0406, "i" }, // І
            { 0x0408, "j" }, // Ј
            { 0x04AE, "y" }, // Ү
            { 0x051A, "q" }, // Ԛ
            { 0x0405, "s" }, // Ѕ

            // Greek that look like Latin
            { 0x03B1, "a" }, // α
            { 0x03BF, "o" }, // ο
            { 0x03C1, "p" }, // ρ
            { 0x03BD, "v" }, // ν
            { 0x0391, "a" }, // Α
            { 0x0392, "b" }, // Β
            { 0x0395, "e" }, // Ε
            { 0x0397, "h" }, // Η
            { 0x0399, "i" }, // Ι
            { 0x039A, "k" }, // Κ
            { 0x039C, "m" }, // Μ
            { 0x039D, "n" }, // Ν
            { 0x039F, "o" }, // Ο
            { 0x03A1, "p" }, // Ρ
            { 0x03A4, "t" }, // Τ
            { 0x03A5, "y" }, // Υ
            { 0x03A7, "x" }, // Χ

            // Common digit/symbol confusables
            { 0x0660, "0" }, // ٠ arabic-indic zero
            { 0x06F0, "0" }, // ۰ extended arabic-indic zero
        };
    }
}
