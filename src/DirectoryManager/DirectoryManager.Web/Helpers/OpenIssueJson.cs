using System.Text.Json;
using System.Text.Json.Serialization;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// The machine-readable twin of the /incidents "Open issues" list (/incidents.json). Same rows
    /// and order as the page: listings whose current status is Scam or Questionable, with the date
    /// they entered that status and the reason note. No reviewer data. Like data.json, Scam
    /// listings never carry their website (no outbound links to scams).
    /// </summary>
    public static class OpenIssueJson
    {
        private static readonly JsonSerializerOptions Options = new ()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static string Build(IEnumerable<OpenIssue> issues, string canonicalDomain, DateTime generatedUtc)
        {
            var domain = (canonicalDomain ?? string.Empty).TrimEnd('/');
            var list = (issues ?? Enumerable.Empty<OpenIssue>())
                .Where(i => OpenIssueBuilder.IsOpenStatus(i.Status) && !string.IsNullOrWhiteSpace(i.DirectoryEntryKey))
                .Select(i => new
                {
                    key = i.DirectoryEntryKey,
                    name = i.Name,
                    profileUrl = $"{domain}/site/{i.DirectoryEntryKey}",
                    website = i.Status == DirectoryStatus.Scam ? null : i.Website,
                    status = i.Status == DirectoryStatus.Scam ? "Scam" : "Questionable",
                    sinceUtc = DateTime.SpecifyKind(i.SinceUtc, DateTimeKind.Utc),
                    reason = i.Reason,
                })
                .ToList();

            var doc = new
            {
                generatedUtc = DateTime.SpecifyKind(generatedUtc, DateTimeKind.Utc),
                description = "Open issues: listings whose current status is Questionable or Scam. A listing leaves this list when its status is set back to Admitted or Verified.",
                pageUrl = $"{domain}/{Controllers.IncidentsController.PagePath}",
                count = list.Count,
                openIssues = list,
            };

            return JsonSerializer.Serialize(doc, Options);
        }
    }
}
