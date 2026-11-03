using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Official reviews are written as "Label: value" lines ("Exchange: 0.02 BTC to 2.87 XMR",
    /// "Fee: ~2.5%", ...) followed by "Comments: ...". This splits them into a compact facts table
    /// plus the comment. When a review isn't written that way it returns <see cref="IsStructured"/>
    /// false, and the page shows the body as before.
    /// </summary>
    public sealed class OfficialReviewFacts
    {
        private const int MinFacts = 3;
        private const int MaxLabelLength = 40;

        private static readonly Regex LabelLine = new (
            @"^\s*([A-Za-z][A-Za-z0-9 ()/&.\-]{0,39}?)\s*:\s*(.*)$",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));

        private OfficialReviewFacts(List<KeyValuePair<string, string>> facts, string? comments, bool isStructured)
        {
            this.Facts = facts;
            this.Comments = comments;
            this.IsStructured = isStructured;
        }

        /// <summary>The "Label: value" pairs in their original order (excluding Comments).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Facts { get; }

        /// <summary>The free-text comment ("Comments: ..." and anything after it), or null.</summary>
        public string? Comments { get; }

        /// <summary>True when the body is mostly "Label: value" lines and can be shown as a table.</summary>
        public bool IsStructured { get; }

        public static OfficialReviewFacts Parse(string? body)
        {
            var facts = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return new OfficialReviewFacts(facts, null, false);
            }

            var lines = body.Replace("\r\n", "\n").Split('\n');
            var comments = new List<string>();
            var inComments = false;
            var stray = 0;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (inComments)
                {
                    comments.Add(line);
                    continue;
                }

                var m = LabelLine.Match(line);
                if (m.Success && m.Groups[1].Value.Length <= MaxLabelLength && IsUrlValue(m))
                {
                    var label = m.Groups[1].Value.Trim();
                    var value = m.Groups[2].Value.Trim();
                    if (label.Equals("Comments", StringComparison.OrdinalIgnoreCase)
                        || label.Equals("Comment", StringComparison.OrdinalIgnoreCase)
                        || label.Equals("Notes", StringComparison.OrdinalIgnoreCase))
                    {
                        inComments = true;
                        if (value.Length > 0)
                        {
                            comments.Add(value);
                        }

                        continue;
                    }

                    if (value.Length > 0)
                    {
                        facts.Add(new KeyValuePair<string, string>(label, value));
                        continue;
                    }
                }

                // A line that isn't "Label: value" right after a fact is that fact wrapping onto
                // the next line (e.g. a long AML result): join it to the fact, not the notes.
                if (facts.Count > 0)
                {
                    var last = facts[^1];
                    facts[^1] = new KeyValuePair<string, string>(last.Key, $"{last.Value} {line}");
                    continue;
                }

                stray++;
                comments.Add(line);
            }

            // Only treat it as structured when the lines really are a list of facts.
            var structured = facts.Count >= MinFacts && stray <= 1;
            var comment = comments.Count > 0 ? string.Join("\n", comments) : null;
            return structured
                ? new OfficialReviewFacts(facts, comment, true)
                : new OfficialReviewFacts(new List<KeyValuePair<string, string>>(), null, false);
        }

        // "Order: https://..." is still a label line; a bare URL with a colon in "https:" is not.
        private static bool IsUrlValue(Match m) =>
            !m.Groups[1].Value.Equals("https", StringComparison.OrdinalIgnoreCase)
            && !m.Groups[1].Value.Equals("http", StringComparison.OrdinalIgnoreCase);
    }
}
