using ExCSS;

namespace DirectoryManager.Utilities.Validation
{
    public class CssValidator
    {
        public static bool IsCssValid(string cssInput)
        {
            // Check for balanced braces
            if (!AreBracesBalanced(cssInput))
            {
                return false;
            }

            var parser = new StylesheetParser();
            var stylesheet = parser.Parse(cssInput);

            // Check if the stylesheet is valid and contains rules
            if (stylesheet == null || !stylesheet.Children.OfType<StyleRule>().Any())
            {
                return false;
            }

            // Loop through the style rules
            foreach (var rule in stylesheet.Children.OfType<StyleRule>())
            {
                var styleDeclaration = rule.Children.OfType<StyleDeclaration>().FirstOrDefault();

                if (styleDeclaration == null || !styleDeclaration.Any())
                {
                    return false; // Invalid if there are no declarations
                }

                // Check each property declaration for correct formatting
                foreach (var declaration in styleDeclaration)
                {
                    // Check if the declaration has a value
                    if (string.IsNullOrWhiteSpace(declaration.Value))
                    {
                        return false; // Invalid if a property has no value
                    }
                }
            }

            return true; // All rules and declarations are valid
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