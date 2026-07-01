using System;
using AngleSharp.Dom;
using Ganss.Xss;

namespace DirectoryManager.DisplayFormatting.Helpers
{
    /// <summary>
    /// Single source of truth for rendering author-written markup safely.
    ///
    ///   Sanitize(...)  -> OUTPUT. Keeps a small allowlist of formatting tags
    ///                     (links, bold, italics, lists, ...) and strips anything
    ///                     that can execute (script, event handlers, javascript:/data: URIs).
    ///
    ///   StripHtml(...) -> INPUT. Removes ALL tags and returns plain text.
    ///                     Use on public submissions so visitor input can never store markup.
    /// </summary>
    public static class ContentSanitizer
    {
        // Safe formatting tags an author may legitimately use in a Note/Description.
        private static readonly string[] AllowedTags =
        {
            "a", "b", "strong", "i", "em", "u", "br", "p", "span", "ul", "ol", "li", "code", "blockquote",
        };

        // Tags that can execute or load/redirect/embed — never allowed, even in the
        // permissive (admin) mode.
        private static readonly string[] DangerousTags =
        {
            "script", "iframe", "frame", "frameset", "object", "embed", "applet",
            "form", "input", "button", "textarea", "select", "option",
            "style", "link", "meta", "base", "svg", "math", "template",
        };

        private static readonly HtmlSanitizer DisplaySanitizer = BuildDisplaySanitizer();
        private static readonly HtmlSanitizer TextOnlySanitizer = BuildTextOnlySanitizer();
        private static readonly HtmlSanitizer PermissiveSanitizer = BuildPermissiveSanitizer();

        /// <summary>
        /// PERMISSIVE sanitizer for admin-authored content: keeps rich HTML (headings,
        /// tables, images, lists, links, formatting, inline styles) so admins can paste
        /// markup as-is, but strips everything executable — &lt;script&gt;, event handlers
        /// (onclick/onerror/...), javascript:/data:/vbscript: URIs, and embed/iframe/form/
        /// style/meta tags. Use on OUTPUT of admin-editable HTML snippets.
        /// </summary>
        /// <returns>Sanitized HTML that still renders rich formatting.</returns>
        public static string SanitizeAllowHtml(string? html)
        {
            return string.IsNullOrWhiteSpace(html)
                ? string.Empty
                : PermissiveSanitizer.Sanitize(html);
        }

        /// <summary>Renders trusted formatting, drops everything executable. Use on output.</summary>
        /// <returns></returns>
        public static string Sanitize(string? html)
        {
            return string.IsNullOrWhiteSpace(html)
                ? string.Empty
                : DisplaySanitizer.Sanitize(html);
        }

        /// <summary>Strips every tag, keeps the text. Use on public input before storing.</summary>
        /// <returns></returns>
        public static string StripHtml(string? html)
        {
            return string.IsNullOrWhiteSpace(html)
                ? string.Empty
                : TextOnlySanitizer.Sanitize(html);
        }

        private static HtmlSanitizer BuildDisplaySanitizer()
        {
            var sanitizer = new HtmlSanitizer();

            // Replace the library's broad defaults with a strict allowlist.
            sanitizer.AllowedTags.Clear();
            foreach (var tag in AllowedTags)
            {
                sanitizer.AllowedTags.Add(tag);
            }

            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("target");
            sanitizer.AllowedAttributes.Add("title");

            // Links may only point at real web schemes. No javascript:, no data:, no mailto-tricks.
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");

            // No inline styles, no data-* attributes.
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowDataAttributes = false;

            // A disallowed tag (e.g. <script>, <img>) is removed together with its contents.
            sanitizer.KeepChildNodes = false;

            // Harden any target="_blank" link against reverse-tabnabbing.
            sanitizer.PostProcessNode += (sender, e) =>
            {
                if (e.Node is IElement element &&
                    string.Equals(element.TagName, "A", StringComparison.OrdinalIgnoreCase))
                {
                    element.SetAttribute("rel", "noopener noreferrer nofollow");
                }
            };

            return sanitizer;
        }

        private static HtmlSanitizer BuildTextOnlySanitizer()
        {
            var sanitizer = new HtmlSanitizer();

            // Nothing is allowed; unwrap every tag and keep the inner text.
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowDataAttributes = false;
            sanitizer.KeepChildNodes = true;

            return sanitizer;
        }

        private static HtmlSanitizer BuildPermissiveSanitizer()
        {
            // Start from the library's BROAD defaults (most formatting/structural tags
            // and attributes are already allowed — headings, tables, images, lists,
            // spans, divs, inline style, class/id, etc.). The Ganss defaults already
            // strip <script>, on* event handlers, and disallow dangerous URI schemes.
            var sanitizer = new HtmlSanitizer();

            // Belt-and-suspenders: explicitly remove anything executable/embedding,
            // regardless of what the library defaults allow now or in the future.
            foreach (var tag in DangerousTags)
            {
                sanitizer.AllowedTags.Remove(tag);
            }

            // Only real, non-executable URL schemes may survive on href/src.
            // (No javascript:, data:, vbscript:, file:, etc.)
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");
            sanitizer.AllowedSchemes.Add("mailto");

            // No data-* attributes; CSS is still sanitized by the library for the
            // inline style attribute (expression()/url(javascript:) are stripped).
            sanitizer.AllowDataAttributes = false;

            // Harden any anchor against reverse-tabnabbing.
            sanitizer.PostProcessNode += (sender, e) =>
            {
                if (e.Node is IElement element &&
                    string.Equals(element.TagName, "A", StringComparison.OrdinalIgnoreCase))
                {
                    element.SetAttribute("rel", "noopener noreferrer nofollow");
                }
            };

            return sanitizer;
        }
    }
}
