using System;
using AngleSharp.Dom;
using Ganss.Xss;

namespace DirectoryManager.Utilities.Validation
{
    /// <summary>
    /// Allowlist sanitizer for the only field that may legitimately contain HTML
    /// (the note field). Everything not explicitly permitted is stripped.
    /// Parser-based (AngleSharp), so encoding/mutation tricks like
    /// "java&#9;script:" are resolved like a browser would and then rejected by
    /// the scheme allowlist — unlike a regex denylist, there is no "missed vector".
    ///
    /// Store the OUTPUT of Sanitize(); never persist or render raw user HTML.
    /// </summary>
    public static class NoteHtmlSanitizer
    {
        private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

        public static string Sanitize(string? input)
            => string.IsNullOrWhiteSpace(input) ? string.Empty : Sanitizer.Sanitize(input);

        private static HtmlSanitizer BuildSanitizer()
        {
            var s = new HtmlSanitizer();

            // Start from a clean slate — the library defaults are broader than a
            // note field needs (they allow img, tables, style, etc.).
            s.AllowedTags.Clear();
            s.AllowedAttributes.Clear();
            s.AllowedCssProperties.Clear();   // no inline CSS surface at all
            s.AllowedAtRules.Clear();
            s.AllowedSchemes.Clear();

            // Minimal formatting set. Trim further if you want; never add
            // script/img/svg/iframe/object/embed/style/form/input/etc.
            foreach (var tag in new[]
            {
                "p", "br", "hr", "b", "strong", "i", "em", "u", "s",
                "ul", "ol", "li", "blockquote", "code", "pre",
                "a", "h3", "h4", "span"
            })
            {
                s.AllowedTags.Add(tag);
            }

            // Only links carry an attribute, and only href/title.
            s.AllowedAttributes.Add("href");
            s.AllowedAttributes.Add("title");

            // The whole reason javascript:/data:/vbscript: can't get through:
            // only these schemes survive. Everything else on an href is removed.
            s.AllowedSchemes.Add("http");
            s.AllowedSchemes.Add("https");
            s.AllowedSchemes.Add("mailto");

            // Drop the element entirely (including its text) when it's not allowed,
            // rather than promoting its children up. Safer default for notes.
            s.KeepChildNodes = false;

            // Harden surviving links.
            s.PostProcessNode += (_, e) =>
            {
                if (e.Node is IElement el &&
                    el.TagName.Equals("A", StringComparison.OrdinalIgnoreCase))
                {
                    el.SetAttribute("rel", "nofollow noopener noreferrer");
                    el.SetAttribute("target", "_blank");
                }
            };

            return s;
        }
    }
}