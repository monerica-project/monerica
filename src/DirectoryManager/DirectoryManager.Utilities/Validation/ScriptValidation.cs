using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Validation
{
    public static class ScriptValidation
    {
        // Matches <script ...> or </script> with any spacing, case-insensitive
        private static readonly Regex ScriptTagRegex =
            new Regex(@"<\s*/?\s*script\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Opening or closing tag for any element that can execute script,
        // load remote content, or break out of an attribute/textarea context.
        // Name-based so harmless tags (e.g. <div>, <b>) and "a < b" text do NOT match.
        private static readonly Regex DangerousTagRegex =
            new Regex(
                @"<\s*/?\s*(script|img|svg|iframe|object|embed|link|base|meta|style|form|textarea|video|audio|source|math|details|template|body|html|input|button|marquee|set|animate|frame|frameset|applet|portal)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Inline event handlers: onerror=, onload=, onclick=, onmouseover=, etc.
        private static readonly Regex EventHandlerRegex =
            new Regex(@"\bon[a-z]+\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Dangerous URI schemes inside attributes/text.
        private static readonly Regex DangerousUriRegex =
            new Regex(@"(javascript|vbscript|data)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // The patterns below are used only by ContainsSuspiciousMarkupAllowingUrls.
        private static readonly Regex AngleBracketUrlRegex =
            new Regex(@"<(https?://[^\s<>""']+)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex HttpUrlRegex =
            new Regex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // A tag that was never closed but already carries an attribute, e.g. "<b onmouseover=".
        private static readonly Regex UnclosedTagWithAttributeRegex =
            new Regex(@"<\s*/?\s*[a-z][a-z0-9]*\s+[a-z-]+\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex EventHandlerExceptOnionRegex =
            new Regex(@"\bon(?!ion\b)[a-z]+\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ScriptUriRegex =
            new Regex(@"(javascript|vbscript)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // A real data URI ("data:text/html,..." / "data:image/png;base64,..."), not the word "data:".
        private static readonly Regex DataUriRegex =
            new Regex(@"\bdata\s*:\s*[a-z]+/[a-z0-9.+-]+\s*[;,]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Tab, CR, LF, form-feed, and NUL are stripped from URLs by browsers
        // before the scheme is parsed, so "java&#9;script:" / "java\tscript:"
        // still executes as "javascript:". Remove them before the URI check
        // so a split scheme name cannot bypass DangerousUriRegex.
        private static readonly Regex UriIgnoredCharsRegex =
            new Regex(@"[\t\r\n\f\u0000]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Original narrow check: presence of a literal &lt;script&gt; tag
        /// (including HTML-encoded variants). Retained for back-compat.
        /// </summary>
        /// <returns></returns>
        public static bool ContainsScriptTag(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            // Decode up to 3 times to handle double-encoded content (e.g., &amp;lt;script&amp;gt;)
            string decoded = MultiDecode(input, 3);
            return ScriptTagRegex.IsMatch(decoded);
        }

        public static bool ContainsScriptTag(object? obj)
        {
            return ScanObject(obj, ContainsScriptTag);
        }

        /// <summary>
        /// Broader check used to reject submissions. Catches script tags AND the
        /// markup-injection patterns that a script-tag-only filter misses:
        /// dangerous tags (e.g. &lt;img&gt;, &lt;/textarea&gt;, &lt;svg&gt;),
        /// inline event handlers (onerror=, onload=...), and javascript:/data: URIs.
        /// </summary>
        /// <returns></returns>
        public static bool ContainsSuspiciousMarkup(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string decoded = MultiDecode(input, 3);

            // Copy with URL-ignorable characters removed, so schemes split by an
            // embedded tab/newline (e.g. "java\tscript:") are still caught.
            string schemeNormalized = UriIgnoredCharsRegex.Replace(decoded, string.Empty);

            return ScriptTagRegex.IsMatch(decoded)
                || DangerousTagRegex.IsMatch(decoded)
                || EventHandlerRegex.IsMatch(decoded)
                || DangerousUriRegex.IsMatch(decoded)
                || DangerousUriRegex.IsMatch(schemeNormalized);
        }

        public static bool ContainsSuspiciousMarkup(object? obj)
        {
            return ScanObject(obj, ContainsSuspiciousMarkup);
        }

        /// <summary>
        /// Variant of <see cref="ContainsSuspiciousMarkup(string?)"/> for free-text fields where
        /// people are expected to paste ordinary http(s) links and write normal prose (e.g. a
        /// report comment). It still rejects every HTML tag, script/dangerous tags, unclosed tags
        /// carrying attributes, inline event handlers and javascript:/vbscript:/data: URIs, but:
        ///   - an http(s) URL is allowed, including one written as &lt;https://example.com&gt;;
        ///   - text inside a URL is not scanned for event handlers (e.g. "?online=1");
        ///   - the word "data:" in a sentence is allowed; only a real data URI ("data:text/html,") is not;
        ///   - "onion=" is not treated as an event handler.
        /// </summary>
        /// <returns></returns>
        public static bool ContainsSuspiciousMarkupAllowingUrls(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string decoded = MultiDecode(input, 3);

            // "<https://example.com/page>" is a plain-text way to write a link, not a tag.
            string unwrapped = AngleBracketUrlRegex.Replace(decoded, "$1");
            string withoutUrls = HttpUrlRegex.Replace(unwrapped, " ");
            string schemeNormalized = UriIgnoredCharsRegex.Replace(unwrapped, string.Empty);

            return HtmlValidation.ContainsHtmlTag(unwrapped)
                || ScriptTagRegex.IsMatch(unwrapped)
                || DangerousTagRegex.IsMatch(unwrapped)
                || UnclosedTagWithAttributeRegex.IsMatch(withoutUrls)
                || EventHandlerExceptOnionRegex.IsMatch(withoutUrls)
                || ScriptUriRegex.IsMatch(unwrapped)
                || ScriptUriRegex.IsMatch(schemeNormalized)
                || DataUriRegex.IsMatch(schemeNormalized);
        }

        // Walks a string, a collection, or all public string properties of an object,
        // applying the supplied per-string predicate.
        private static bool ScanObject(object? obj, System.Func<string?, bool> stringCheck)
        {
            if (obj is null)
            {
                return false;
            }

            if (obj is string s)
            {
                return stringCheck(s);
            }

            if (obj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (ScanObject(item, stringCheck))
                    {
                        return true;
                    }
                }
            }

            var props = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in props)
            {
                if (prop.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(obj) as string;
                    if (stringCheck(value))
                    {
                        return true;
                    }
                }
                else if (typeof(IEnumerable<string>).IsAssignableFrom(prop.PropertyType))
                {
                    if (prop.GetValue(obj) is IEnumerable<string> strings)
                    {
                        foreach (var str in strings)
                        {
                            if (stringCheck(str))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static string MultiDecode(string input, int times)
        {
            string current = input;
            for (int i = 0; i < times; i++)
            {
                string decoded = System.Net.WebUtility.HtmlDecode(current);
                if (decoded == current)
                {
                    break;
                }

                current = decoded;
            }

            return current;
        }
    }
}