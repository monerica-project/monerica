namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Builds a listing's address from its parts (street, city, state/region, postal code).
    /// The country is kept separately (CountryCode) and is not part of these strings.
    /// </summary>
    public static class PostalAddress
    {
        /// <summary>
        /// One line, e.g. "1007 N Orange St, Wilmington, Delaware 19801".
        /// Empty parts are skipped; returns null when every part is empty.
        /// </summary>
        public static string? Format(string? street, string? city, string? stateRegion, string? postalCode)
        {
            var parts = new List<string>();
            Add(parts, street);
            Add(parts, city);

            var state = Clean(stateRegion);
            var postal = Clean(postalCode);
            if (state != null && postal != null)
            {
                parts.Add(state + " " + postal);
            }
            else
            {
                Add(parts, state);
                Add(parts, postal);
            }

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>True when any part of the address is filled in.</summary>
        public static bool HasAny(string? street, string? city, string? stateRegion, string? postalCode) =>
            Clean(street) != null || Clean(city) != null || Clean(stateRegion) != null || Clean(postalCode) != null;

        /// <summary>Trims a part; whitespace-only becomes null.</summary>
        public static string? Clean(string? value)
        {
            var v = value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        private static void Add(List<string> parts, string? value)
        {
            var v = Clean(value);
            if (v != null)
            {
                parts.Add(v);
            }
        }
    }
}
