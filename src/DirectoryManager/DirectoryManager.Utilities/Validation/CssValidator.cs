using System.Text.RegularExpressions;

namespace DirectoryManager.Utilities.Validation
{
    public class CssValidator
    {
        // Basic regex pattern to match CSS selectors, properties, and values.
        private static readonly string BasicCssPattern = @"^(\s*[a-zA-Z_\-\#\.][a-zA-Z0-9_\-\#\.]*\s*\{\s*([a-zA-Z\-]+\s*:\s*[^\{\};]+\s*;\s*)+\}\s*)+$";

        // Validate entire CSS block by combining regex and logic.
        public static bool ValidateCss(string css)
        {
            // Remove newlines and extra spaces for easier validation
            css = css.Trim();

            // Strip out all comments using a regex
            css = StripCssComments(css);

            // Break CSS down into individual blocks and validate each one
            var cssBlocks = Regex.Matches(css, BasicCssPattern);

            foreach (Match block in cssBlocks)
            {
                var blockText = block.Value.Trim();

                // Validate selectors
                var selector = blockText.Substring(0, blockText.IndexOf("{")).Trim();
                if (!ValidateSelector(selector))
                {
                    return false; // Invalid selector
                }

                // Validate properties inside the block (we already know structure is fine via regex)
                var properties = blockText.Substring(blockText.IndexOf("{") + 1, blockText.IndexOf("}") - blockText.IndexOf("{") - 1);
                if (!ValidateProperties(properties))
                {
                    return false; // Invalid properties
                }
            }

            return cssBlocks.Count > 0; // Ensure some valid CSS blocks exist
        }

        // Strips out all comments from the CSS
        private static string StripCssComments(string css)
        {
            // This regex matches CSS comments like /* comment */
            return Regex.Replace(css, @"/\*[\s\S]*?\*/", string.Empty);
        }

        // Selector validation (no starting with digits, no invalid characters)
        private static bool ValidateSelector(string selector)
        {
            // Split by commas for multiple selectors
            var individualSelectors = selector.Split(',');

            foreach (var s in individualSelectors)
            {
                var trimmedSelector = s.Trim();

                // Ensure the selector does not start with a digit after a class (.) or id (#)
                if (trimmedSelector.StartsWith(".") || trimmedSelector.StartsWith("#"))
                {
                    if (char.IsDigit(trimmedSelector[1]))
                    {
                        return false;
                    }
                }

                // Ensure it starts with valid characters (a letter, #, ., or *)
                if (!Regex.IsMatch(trimmedSelector, @"^[a-zA-Z\.\#\*][a-zA-Z0-9\.\#\-_]*$"))
                {
                    return false;
                }
            }

            return true;
        }

        // Property validation (ensure properties and values are well-formed)
        private static bool ValidateProperties(string properties)
        {
            var propertyPairs = properties.Split(';');

            foreach (var pair in propertyPairs)
            {
                if (string.IsNullOrWhiteSpace(pair))
                {
                    continue; // Skip empty lines or the last semicolon
                }

                var keyValue = pair.Split(':');
                if (keyValue.Length != 2)
                {
                    return false; // Invalid property
                }

                var property = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                // Check property name is valid (alphanumeric or hyphenated)
                if (!Regex.IsMatch(property, @"^[a-zA-Z\-]+$"))
                {
                    return false;
                }

                // Ensure the value isn't empty
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
