﻿using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    internal static class ModelComparisonHelpers
    {
        public static string CompareEntries(DirectoryEntry entry, Submission submission)
        {
            if (entry == null || submission == null)
            {
                return "<p>Either the DirectoryEntry or the Submission is null.</p>";
            }

            // Helper function to compare strings, treating null and empty as equivalent.
            static bool NotEqualTrimmed(string? a, string? b)
            {
                string normalizedA = NormalizeString(a);
                string normalizedB = NormalizeString(b);
                return !string.Equals(normalizedA, normalizedB, StringComparison.OrdinalIgnoreCase);
            }

            // Normalize strings by trimming and treating null, empty, or whitespace-only as empty.
            static string NormalizeString(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

            var differences = new List<string>();

            void AddDifference(string label, object? entryValue, object? submissionValue)
            {
                differences.Add(
                    $"<p><strong>{label}:</strong><br>" +
                    $"<em>Entry:</em><br> {FormatValue(entryValue)}<br>" +
                    $"<em>Submission:</em><br> {FormatValue(submissionValue)}</p>");
            }

            // Format null or non-string values safely.
            static string FormatValue(object? value) => value?.ToString() ?? "null";

            // Compare properties and add differences to the list.
            if (NotEqualTrimmed(entry.Name, submission.Name))
            {
                AddDifference("Name", entry.Name, submission.Name);
            }

            if (NotEqualTrimmed(entry.Link, submission.Link))
            {
                AddDifference("Link", entry.Link, submission.Link);
            }

            if (NotEqualTrimmed(entry.Link2, submission.Link2))
            {
                AddDifference("Link2", entry.Link2, submission.Link2);
            }

            if (NotEqualTrimmed(entry.Description, submission.Description))
            {
                AddDifference("Description", entry.Description, submission.Description);
            }

            if (NotEqualTrimmed(entry.Location, submission.Location))
            {
                AddDifference("Location", entry.Location, submission.Location);
            }

            if (NotEqualTrimmed(entry.CountryCode, submission.CountryCode))
            {
                AddDifference("Country", entry.CountryCode, submission.CountryCode);
            }

            if (NotEqualTrimmed(entry.Processor, submission.Processor))
            {
                AddDifference("Processor", entry.Processor, submission.Processor);
            }

            if (NotEqualTrimmed(entry.Note, submission.Note))
            {
                AddDifference("Note", entry.Note, submission.Note);
            }

            if (NotEqualTrimmed(entry.Contact, submission.Contact))
            {
                AddDifference("Contact", entry.Contact, submission.Contact);
            }

            if (NotEqualTrimmed(entry.PgpKey, submission.PgpKey))
            {
                AddDifference("PgpKey", entry.PgpKey, submission.PgpKey);
            }

            // Compare SubCategory and Category names safely.
            if (entry.SubCategoryId != submission.SubCategoryId)
            {
                string entrySubCategory = $"{FormatValue(entry.SubCategory?.Category?.Name)} > {FormatValue(entry.SubCategory?.Name)}";
                string submissionSubCategory = $"{FormatValue(submission.SubCategory?.Category?.Name)} > {FormatValue(submission.SubCategory?.Name)}";
                AddDifference("Subcategory", entrySubCategory, submissionSubCategory);
            }

            if (entry.Tags != submission.Tags)
            {
                AddDifference("Tags", entry.Tags, submission.Tags);
            }

            if (entry.DirectoryStatus != submission.DirectoryStatus)
            {
                AddDifference("Directory Status", entry.DirectoryStatus, submission.DirectoryStatus);
            }

            return differences.Count > 0
                ? string.Join(Environment.NewLine, differences)
                : "<p>No differences found.</p>";
        }
    }
}