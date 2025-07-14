namespace DirectoryManager.DisplayFormatting.Helpers
{
    public static class StringHelpers
    {
        /// <summary>
        /// Truncates the input to maxLength characters, appending “…” if it was longer.
        /// </summary>
        public static string Truncate(this string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            {
                return input ?? string.Empty;
            }

            return input.Substring(0, maxLength) + "…";
        }
    }
}
