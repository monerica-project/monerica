using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Shows a stored email's HTML on the admin send-log detail page without trusting it: the HTML
    /// goes ONLY into the <c>srcdoc</c> attribute of an iframe with <c>sandbox=""</c> (no scripts,
    /// forms, popups, top navigation or same-origin access), attribute-encoded by the HTML encoder,
    /// and starts with its own Content-Security-Policy that blocks every remote load (images, styles,
    /// fonts, frames). It also inherits the page's policy (script-src 'none', frame-src 'none'), so a
    /// click on a link inside the preview can't navigate the frame anywhere.
    /// </summary>
    public static class EmailPreviewHelper
    {
        /// <summary>The policy placed at the top of the previewed document: inline styles and data:
        /// images only, nothing fetched from the network.</summary>
        public const string PreviewCsp =
            "default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:; " +
            "media-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'";

        private const string CspMeta = "<meta http-equiv=\"Content-Security-Policy\" content=\"" + PreviewCsp + "\">";

        /// <summary>The document given to srcdoc: the email HTML with the CSP meta tag first
        /// (after a leading doctype, so the email keeps its rendering mode).</summary>
        /// <returns>The srcdoc document text (NOT yet attribute-encoded).</returns>
        public static string BuildSrcdoc(string? html)
        {
            html ??= string.Empty;
            var trimmed = html.TrimStart('﻿', ' ', '\t', '\r', '\n');
            if (trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase))
            {
                var end = trimmed.IndexOf('>');
                if (end > 0)
                {
                    return trimmed.Substring(0, end + 1) + CspMeta + trimmed.Substring(end + 1);
                }
            }

            return CspMeta + html;
        }

        /// <summary>The sandboxed iframe element; every attribute value is HTML-encoded.</summary>
        /// <returns>The iframe markup.</returns>
        public static IHtmlContent BuildIframe(string? html, string title)
        {
            var tag = new TagBuilder("iframe");
            tag.Attributes["sandbox"] = string.Empty;
            tag.Attributes["referrerpolicy"] = "no-referrer";
            tag.Attributes["loading"] = "lazy";
            tag.Attributes["title"] = title;
            tag.Attributes["class"] = "email-preview-frame";
            tag.Attributes["style"] = "width:100%;height:640px;border:1px solid #888;background:#fff;";
            tag.Attributes["srcdoc"] = BuildSrcdoc(html);
            return tag;
        }

        /// <summary>The iframe as a string (for tests).</summary>
        /// <returns>The encoded markup.</returns>
        public static string RenderIframe(string? html, string title = "Email preview")
        {
            using var writer = new StringWriter();
            BuildIframe(html, title).WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }
    }
}
