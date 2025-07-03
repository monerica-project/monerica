using System.Linq;
using System.Text;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Encodings.Web;

namespace DirectoryManager.Web.Helpers
{
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Truncates the input HTML to a maximum of <paramref name="maxLength"/> text characters,
        /// preserving tags and closing them properly.
        /// </summary>
        public static IHtmlContent TruncateHtml(
            this IHtmlHelper html,
            string sourceHtml,
            int maxLength)
        {
            if (string.IsNullOrEmpty(sourceHtml) || maxLength <= 0)
            {
                return HtmlString.Empty;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(sourceHtml);

            var sb = new StringBuilder();
            int current = 0;

            void Walk(HtmlNode node)
            {
                if (current >= maxLength) return;

                switch (node.NodeType)
                {
                    case HtmlNodeType.Text:
                        var text = HtmlEntity.DeEntitize(node.InnerText);
                        var remain = maxLength - current;
                        if (text.Length <= remain)
                        {
                            sb.Append(HtmlEncoder.Default.Encode(text));
                            current += text.Length;
                        }
                        else
                        {
                            sb.Append(HtmlEncoder.Default.Encode(text.Substring(0, remain)));
                            current = maxLength;
                        }
                        break;

                    case HtmlNodeType.Element:
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

                        // children
                        foreach (var child in node.ChildNodes)
                        {
                            Walk(child);
                            if (current >= maxLength) break;
                        }

                        // close tag
                        sb.Append("</").Append(node.Name).Append('>');
                        break;
                }
            }

            foreach (var child in doc.DocumentNode.ChildNodes)
            {
                Walk(child);
                if (current >= maxLength) break;
            }

            // if we hit the limit, add ellipsis
            if (current >= maxLength)
            {
                sb.Append("…");
            }

            return new HtmlString(sb.ToString());
        }
    }
}
