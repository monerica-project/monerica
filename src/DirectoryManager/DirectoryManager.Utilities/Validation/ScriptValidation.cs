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

        /// <summary>
        /// Checks a raw string for the presence of a script tag (including HTML-encoded variants).
        /// </summary>
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

        /// <summary>
        /// Checks an object. If it's a string, checks the string. Otherwise scans all public string properties.
        /// Also walks simple IEnumerable collections.
        /// </summary>
        public static bool ContainsScriptTag(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (obj is string s)
            {
                return ContainsScriptTag(s);
            }

            // If the object is a collection, scan elements
            if (obj is IEnumerable enumerable && obj is not string)
            {
                foreach (var item in enumerable)
                {
                    if (ContainsScriptTag(item))
                    {
                        return true;
                    }
                }
            }

            // Scan public instance string properties
            var props = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in props)
            {
                if (prop.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(obj) as string;
                    if (ContainsScriptTag(value))
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
                            if (ContainsScriptTag(str))
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