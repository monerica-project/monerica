using System.Globalization;
using System.Text;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Normalizes free-text input and removes invisible / control / direction-changing
    /// characters that would otherwise let user-submitted text:
    ///   - spoof its own appearance (bidi overrides, e.g. U+202E),
    ///   - hide content or evade word filters (zero-width spaces/joiners, soft hyphen, BOM),
    ///   - store in multiple byte forms that defeat equality / uniqueness checks (no NFC).
    ///
    /// This intentionally does NOT block non-Latin scripts. Russian, Chinese, Arabic, etc.
    /// are all legitimate. It only strips characters that have no business in a display
    /// name / URL / review body and exist mainly to deceive.
    /// </summary>
    public static class UnicodeSanitizer
    {
        /// <summary>
        /// Clean a single-line value (names, titles, link display text). Line breaks and
        /// tabs are converted to spaces and runs of whitespace are collapsed.
        /// </summary>
        /// <returns></returns>
        public static string CleanSingleLine(string? input)
            => Clean(input, allowLineBreaks: false);

        /// <summary>
        /// Clean a multi-line value (review/reply bodies, notes). Line breaks are preserved
        /// but normalized to "\n"; trailing whitespace on each line is trimmed.
        /// </summary>
        /// <returns></returns>
        public static string CleanMultiLine(string? input)
            => Clean(input, allowLineBreaks: true);

        /// <summary>
        /// Removes emoji / pictographic characters (and their joiners and variation selectors).
        /// Emojis are not allowed in review replies and comments; any already present are stripped
        /// on save. Ordinary text — letters, digits, punctuation, line breaks — is preserved.
        /// </summary>
        public static string StripEmoji(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);
            foreach (var rune in input.EnumerateRunes())
            {
                if (!IsEmoji(rune.Value))
                {
                    sb.Append(rune.ToString());
                }
            }

            // An emoji between words can leave a double space behind — collapse those (not newlines).
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "[ \\t]{2,}", " ");
        }

        private static bool IsEmoji(int cp) =>
            (cp >= 0x1F000 && cp <= 0x1FAFF)   // emoji, pictographs, faces, flags, symbols
            || (cp >= 0x2600 && cp <= 0x27BF)  // misc symbols + dingbats (sun, coffee, check, heart, arrows)
            || (cp >= 0x2300 && cp <= 0x23FF)  // misc technical (watch, alarm, hourglass)
            || (cp >= 0x2B00 && cp <= 0x2BFF)  // misc symbols & arrows (star, squares)
            || (cp >= 0xFE00 && cp <= 0xFE0F)  // variation selectors (emoji presentation)
            || cp == 0x200D                    // zero-width joiner (emoji sequences)
            || cp == 0x20E3;                   // combining enclosing keycap

        public static string Clean(string? input, bool allowLineBreaks)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input.Length);

            foreach (var rune in input.EnumerateRunes())
            {
                int cp = rune.Value;

                // Carriage returns are dropped so that a CRLF ("\r\n") collapses to a
                // single break via the following LF, instead of being counted twice
                // (which previously doubled every line break).
                if (cp == '\r')
                {
                    continue;
                }

                // Line breaks: normalize LF and the Unicode line/paragraph separators
                // (U+2028/U+2029) to a single '\n', or to a space if single-line.
                if (cp == '\n' || cp == 0x2028 || cp == 0x2029)
                {
                    sb.Append(allowLineBreaks ? '\n' : ' ');
                    continue;
                }

                // Tabs -> space.
                if (cp == '\t')
                {
                    sb.Append(' ');
                    continue;
                }

                var cat = Rune.GetUnicodeCategory(rune);

                switch (cat)
                {
                    // Drop all other control and format characters. This is what removes
                    // bidi overrides (U+202A-202E, U+2066-2069, U+200E/F), zero-width
                    // characters (U+200B-200D), the BOM/ZWNBSP (U+FEFF), the soft hyphen
                    // (U+00AD), the Arabic letter mark (U+061C), etc.
                    case UnicodeCategory.Control:
                    case UnicodeCategory.Format:
                    case UnicodeCategory.Surrogate: // unpaired surrogates
                    case UnicodeCategory.PrivateUse:
                    case UnicodeCategory.OtherNotAssigned: // unassigned / noncharacters
                        continue;

                    // Any space separator (incl. NBSP U+00A0, narrow/figure spaces,
                    // ideographic space U+3000) collapses to a normal ASCII space.
                    case UnicodeCategory.SpaceSeparator:
                        sb.Append(' ');
                        continue;

                    default:
                        // U+FFFD replacement char and the noncharacters are not always
                        // categorized above; strip them explicitly.
                        if (cp == 0xFFFD || cp == 0xFFFE || cp == 0xFFFF ||
                            (cp >= 0xFDD0 && cp <= 0xFDEF) ||
                            ((cp & 0xFFFF) == 0xFFFE) || ((cp & 0xFFFF) == 0xFFFF))
                        {
                            continue;
                        }

                        sb.Append(rune.ToString());
                        continue;
                }
            }

            // Canonical composition so the same visual string has one byte form.
            var normalized = sb.ToString().Normalize(NormalizationForm.FormC);

            // Collapse whitespace.
            return allowLineBreaks
                ? CollapsePreservingLines(normalized)
                : CollapseSingleLine(normalized);
        }

        /// <summary>
        /// True if the string still contains any of the characters this sanitizer removes.
        /// Useful for asserting in tests or for "was this tampered with?" checks.
        /// </summary>
        /// <returns></returns>
        public static bool ContainsSuspectCharacters(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            foreach (var rune in input.EnumerateRunes())
            {
                int cp = rune.Value;
                if (cp == '\t' || cp == '\n' || cp == '\r')
                {
                    continue;
                }

                var cat = Rune.GetUnicodeCategory(rune);
                if (cat is UnicodeCategory.Control
                        or UnicodeCategory.Format
                        or UnicodeCategory.Surrogate
                        or UnicodeCategory.PrivateUse
                        or UnicodeCategory.OtherNotAssigned)
                {
                    return true;
                }

                if (cp == 0xFFFD || cp == 0x2028 || cp == 0x2029)
                {
                    return true;
                }
            }

            return false;
        }

        private static string CollapseSingleLine(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool prevSpace = false;
            foreach (var ch in s)
            {
                if (ch == ' ')
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                    }

                    prevSpace = true;
                }
                else
                {
                    sb.Append(ch);
                    prevSpace = false;
                }
            }

            return sb.ToString().Trim();
        }

        private static string CollapsePreservingLines(string s)
        {
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = CollapseSingleLine(lines[i]);
            }

            // Trim leading/trailing blank lines, keep internal structure.
            return string.Join('\n', lines).Trim('\n');
        }
    }
}
