using System.Text;
using System.Text.Encodings.Web;
using DirectoryManager.Utilities.Helpers;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Helpers
{
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Truncates HTML (or plain text) to a maximum of <paramref name="maxLength"/> text characters.
        /// - If the source is plain text (no HTML elements), truncates at the nearest word using TextHelper.TruncateAtWord().
        /// - If the source is HTML, preserves tags/structure and avoids cutting inside a word within a text node.
        /// Always returns valid HTML and appends a single ellipsis (… ) when truncated.
        /// </summary>
        /// <returns>The html string.</returns>
        public static IHtmlContent TruncateHtml(this IHtmlHelper html, string sourceHtml, int maxLength)
        {
            if (string.IsNullOrEmpty(sourceHtml) || maxLength <= 0)
            {
                return HtmlString.Empty;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(sourceHtml);

            // If there are no HTML element nodes, treat as plain text
            bool hasElements = doc.DocumentNode.Descendants().Any(n => n.NodeType == HtmlNodeType.Element);
            if (!hasElements)
            {
                string truncated = StringHelpers.TruncateAtWord(sourceHtml, maxLength);

                // Encode because this branch is plain text
                return new HtmlString(HtmlEncoder.Default.Encode(truncated));
            }

            var sb = new StringBuilder();
            int current = 0;
            bool truncatedAny = false;

            // HTML void/self-closing elements that should not have an end tag
            var voidElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "area", "base", "br", "col", "embed", "hr", "img", "input", "keygen", "link",
                "meta", "param", "source", "track", "wbr"
            };

            // choose a word boundary at or before limit; fallback to hard cut
            static int LastWordBreak(string text, int limit)
            {
                if (limit <= 0)
                {
                    return 0;
                }

                int end = Math.Min(limit, text.Length);
                for (int i = end - 1; i >= 0; i--)
                {
                    if (char.IsWhiteSpace(text[i]))
                    {
                        return i; // cut BEFORE this whitespace
                    }
                }
                return end; // no whitespace; hard cut
            }

            void Walk(HtmlNode node)
            {
                if (current >= maxLength)
                {
                    return;
                }

                switch (node.NodeType)
                {
                    case HtmlNodeType.Text:
                        {
                            var raw = HtmlEntity.DeEntitize(node.InnerText); // as text
                            if (string.IsNullOrEmpty(raw))
                            {
                                return;
                            }

                            int remain = maxLength - current;
                            if (raw.Length <= remain)
                            {
                                sb.Append(HtmlEncoder.Default.Encode(raw));
                                current += raw.Length;
                            }
                            else
                            {
                                int cut = LastWordBreak(raw, remain);
                                if (cut == 0 && remain > 0)
                                {
                                    cut = remain; // no whitespace; hard cut
                                }

                                sb.Append(HtmlEncoder.Default.Encode(raw.Substring(0, cut)));
                                current = maxLength;
                                truncatedAny = true;
                            }

                            break;
                        }

                    case HtmlNodeType.Element:
                        {
                            bool isVoid = voidElements.Contains(node.Name);

                            // open tag
                            sb.Append('<').Append(node.Name);
                            foreach (var attr in node.Attributes)
                            {
                                sb.Append(' ')
                                  .Append(attr.Name)
                                  .Append("=\"")
                                  .Append(HtmlEncoder.Default.Encode(attr.Value))
                                  .Append('"');
                            }

                            sb.Append('>');

                            // children (skip for void elements)
                            if (!isVoid)
                            {
                                foreach (var child in node.ChildNodes)
                                {
                                    Walk(child);
                                    if (current >= maxLength)
                                    {
                                        break;
                                    }
                                }

                                // close tag for non-voids
                                sb.Append("</").Append(node.Name).Append('>');
                            }

                            break;
                        }

                        // comments, etc. are ignored in output
                }
            }

            foreach (var child in doc.DocumentNode.ChildNodes)
            {
                Walk(child);
                if (current >= maxLength)
                {
                    break;
                }
            }

            if (truncatedAny)
            {
                sb.Append('…');
            }

            return new HtmlString(sb.ToString());
        }
    }
}
