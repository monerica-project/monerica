using System;
using System.Collections.Generic;
using System.Linq;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Helpers
{
    internal static class ModelComparisonHelpers
    {
        public static string CompareEntries(
            DirectoryEntry entry,
            Submission submission,
            IReadOnlyList<string>? entryTagNames = null,
            IReadOnlyList<string>? selectedTagNames = null,
            IReadOnlyList<string>? entryRelatedLinks = null)
        {
            if (entry == null || submission == null)
            {
                return "<p>Either the DirectoryEntry or the Submission is null.</p>";
            }

            static string NormalizeString(string? value)
            {
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }

            static bool NotEqualTrimmed(string? a, string? b)
            {
                return !string.Equals(NormalizeString(a), NormalizeString(b), StringComparison.OrdinalIgnoreCase);
            }

            static string FormatValue(object? value)
            {
                return value?.ToString() ?? "null";
            }

            static List<string> NormalizeLinks(IEnumerable<string?>? links, int max = 3)
            {
                return (links ?? Array.Empty<string?>())
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(max)
                    .ToList();
            }

            var differences = new List<string>();

            void AddDifference(string label, object? entryValue, object? submissionValue)
            {
                differences.Add(
                    $"<p><strong>{label}:</strong><br>" +
                    $"<em>Entry:</em><br> {FormatValue(entryValue)}<br>" +
                    $"<em>Submission:</em><br> {FormatValue(submissionValue)}</p>");
            }

            // -----------------------------
            // Core fields
            // -----------------------------
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

            if (NotEqualTrimmed(entry.ProofLink, submission.ProofLink))
            {
                AddDifference("ProofLink", entry.ProofLink, submission.ProofLink);
            }

            if (NotEqualTrimmed(entry.VideoLink, submission.VideoLink))
            {
                AddDifference("VideoLink", entry.VideoLink, submission.VideoLink);
            }

            // SubCategory display
            if (entry.SubCategoryId != submission.SubCategoryId)
            {
                string entrySubCategory =
                    $"{FormatValue(entry.SubCategory?.Category?.Name)} &gt; {FormatValue(entry.SubCategory?.Name)}";
                string submissionSubCategory =
                    $"{FormatValue(submission.SubCategory?.Category?.Name)} &gt; {FormatValue(submission.SubCategory?.Name)}";

                AddDifference("Subcategory", entrySubCategory, submissionSubCategory);
            }

            // Status
            if (entry.DirectoryStatus != submission.DirectoryStatus)
            {
                AddDifference("Directory Status", entry.DirectoryStatus, submission.DirectoryStatus);
            }

            if (entry.FoundedDate != submission.FoundedDate)
            {
                AddDifference("Founded Date", entry.FoundedDate, submission.FoundedDate);
            }

            // -----------------------------
            // Tags diff
            // -----------------------------
            var entrySet = new HashSet<string>(
                (entryTagNames ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var selectedSet = new HashSet<string>(
                (selectedTagNames ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);

            if (!entrySet.SetEquals(selectedSet))
            {
                var added = selectedSet.Except(entrySet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
                var removed = entrySet.Except(selectedSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

                string entryTagsDisplay = entrySet.Count == 0
                    ? "<i>(none)</i>"
                    : string.Join(", ", entrySet.OrderBy(x => x));

                string selectedTagsDisplay = selectedSet.Count == 0
                    ? "<i>(none)</i>"
                    : string.Join(", ", selectedSet.OrderBy(x => x));

                string addedDisplay = added.Count == 0 ? "<i>(none)</i>" : string.Join(", ", added);
                string removedDisplay = removed.Count == 0 ? "<i>(none)</i>" : string.Join(", ", removed);

                differences.Add(
                    "<p><strong>Tags:</strong><br>" +
                    $"<em>Entry:</em><br> {entryTagsDisplay}<br>" +
                    $"<em>User selected:</em><br> {selectedTagsDisplay}<br>" +
                    $"<em>Added:</em><br> {addedDisplay}<br>" +
                    $"<em>Removed:</em><br> {removedDisplay}</p>");
            }

            // -----------------------------
            // Related/Additional Links diff
            // -----------------------------
            var entryLinks = NormalizeLinks(entryRelatedLinks, max: 3);
            var submissionLinks = NormalizeLinks(submission.RelatedLinks, max: 3);

            var entryLinksSet = entryLinks.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var submissionLinksSet = submissionLinks.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!entryLinksSet.SetEquals(submissionLinksSet))
            {
                var added = submissionLinks.Where(x => !entryLinksSet.Contains(x)).ToList();
                var removed = entryLinks.Where(x => !submissionLinksSet.Contains(x)).ToList();

                string entryDisplay = entryLinks.Count == 0
                    ? "<i>(none)</i>"
                    : string.Join("<br>", entryLinks.Select(x =>
                        $"<a href=\"{x}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{x}</a>"));

                string submissionDisplay = submissionLinks.Count == 0
                    ? "<i>(none)</i>"
                    : string.Join("<br>", submissionLinks.Select(x =>
                        $"<a href=\"{x}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{x}</a>"));

                string addedDisplay = added.Count == 0
                    ? "<i>(none)</i>"
                    : string.Join("<br>", added.Select(x =>
                        $"<a href=\"{x}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">{x}</a>"));

                string removedDisplay = removed.Count == 0
                    ? "<i>(none)</i>"
                    : string.Join("<br>", removed.Select(x => $"<span>{x}</span>"));

                differences.Add(
                    "<p><strong>Related Links:</strong><br>" +
                    $"<em>Entry:</em><br> {entryDisplay}<br>" +
                    $"<em>Submission:</em><br> {submissionDisplay}<br>" +
                    $"<em>Added:</em><br> {addedDisplay}<br>" +
                    $"<em>Removed:</em><br> {removedDisplay}</p>");
            }

            if (differences.Count > 0)
            {
                return string.Join(Environment.NewLine, differences);
            }

            return "<p>No differences found.</p>";
        }
    }
}
