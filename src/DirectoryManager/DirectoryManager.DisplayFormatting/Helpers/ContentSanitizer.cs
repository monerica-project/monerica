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

        private static readonly HtmlSanitizer DisplaySanitizer = BuildDisplaySanitizer();
        private static readonly HtmlSanitizer TextOnlySanitizer = BuildTextOnlySanitizer();

        /// <summary>Renders trusted formatting, drops everything executable. Use on output.</summary>
        public static string Sanitize(string? html)
        {
            return string.IsNullOrWhiteSpace(html)
                ? string.Empty
                : DisplaySanitizer.Sanitize(html);
        }

        /// <summary>Strips every tag, keeps the text. Use on public input before storing.</summary>
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
    }
}
