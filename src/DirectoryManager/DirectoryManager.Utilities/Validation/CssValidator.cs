using System.Text.RegularExpressions;
using ExCSS;

namespace DirectoryManager.Utilities.Validation
{
    public class CssValidator
    {
        public static bool IsCssValid(string cssInput)
        {
            if (cssInput == string.Empty)
            {
                return false;
            }

            // Remove <style> tags if present
            cssInput = StripStyleTags(cssInput);

            // Check for balanced braces
            if (!AreBracesBalanced(cssInput))
            {
                return false;
            }

            var parser = new StylesheetParser();
            var stylesheet = parser.Parse(cssInput);

            if (stylesheet == null)
            {
                return false;
            }

            foreach (var rule in stylesheet.Children.OfType<StyleRule>())
            {
                var styleDeclaration = rule.Children.OfType<StyleDeclaration>().FirstOrDefault();

                if (styleDeclaration == null || !styleDeclaration.Declarations.Any())
                {
                    return false;
                }

                foreach (var declaration in styleDeclaration)
                {
                    if (string.IsNullOrWhiteSpace(declaration.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static string StripStyleTags(string input)
        {
            return Regex.Replace(input, "<style[^>]*>|</style>", "", RegexOptions.IgnoreCase).Trim();
        }

        private static bool AreBracesBalanced(string input)
        {
            int openBraces = 0;
            foreach (var ch in input)
            {
                if (ch == '{')
                {
                    openBraces++;
                }
                else if (ch == '}')
                {
                    openBraces--;

                    if (openBraces < 0)
                    {
                        // More closing braces than opening
                        return false;
                    }
                }
            }

            return openBraces == 0; // Valid if open and close braces match
        }
    }
}