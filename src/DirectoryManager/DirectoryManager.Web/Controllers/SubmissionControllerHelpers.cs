using DirectoryManager.Data.Models;

internal static class SubmissionControllerHelpers
{

    public static string CompareEntries(DirectoryEntry entry, Submission submission)
    {
        if (entry == null || submission == null)
        {
            return "Either the DirectoryEntry or the Submission is null.";
        }

        // Helper function to compare trimmed strings, considering null values.
        static bool NotEqualTrimmed(string? a, string? b)
        {
            return (a?.Trim() ?? string.Empty) != (b?.Trim() ?? string.Empty);
        }

        var differences = new List<string>();

        if (NotEqualTrimmed(entry.Name, submission.Name))
        {
            differences.Add($"Different Name: {entry.Name ?? "null"} vs {submission.Name ?? "null"}.");
        }

        if (NotEqualTrimmed(entry.Link, submission.Link))
        {
            differences.Add($"Different Link: {entry.Link ?? "null"} vs {submission.Link ?? "null"}.");
        }

        if (NotEqualTrimmed(entry.Link2, submission.Link2)) differences.Add($"Different Link2: {entry.Link2 ?? "null"} vs {submission.Link2 ?? "null"}.");
        if (NotEqualTrimmed(entry.Description, submission.Description)) differences.Add($"Different Description: {entry.Description ?? "null"} vs {submission.Description ?? "null"}.");
        if (NotEqualTrimmed(entry.Location, submission.Location)) differences.Add($"Different Location: {entry.Location ?? "null"} vs {submission.Location ?? "null"}.");
        if (NotEqualTrimmed(entry.Processor, submission.Processor)) differences.Add($"Different Processor: {entry.Processor ?? "null"} vs {submission.Processor ?? "null"}.");
        if (NotEqualTrimmed(entry.Note, submission.Note)) differences.Add($"Different Note: {entry.Note ?? "null"} vs {submission.Note ?? "null"}.");
        if (NotEqualTrimmed(entry.Contact, submission.Contact)) differences.Add($"Different Contact: {entry.Contact ?? "null"} vs {submission.Contact ?? "null"}.");
        if (entry.SubCategoryId != submission.SubCategoryId) differences.Add($"Different SubCategoryId: {entry.SubCategoryId} vs {submission.SubCategoryId}.");
        if (entry.DirectoryStatus != submission.DirectoryStatus) differences.Add($"Different DirectoryStatus: {entry.DirectoryStatus} vs {submission.DirectoryStatus}.");

        return differences.Count > 0 ? string.Join(Environment.NewLine, differences) : "No differences found.";
    }
}