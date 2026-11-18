using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Builds the /incidents "Open issues" list. An issue is open while a listing's CURRENT
    /// status is Scam or Questionable; setting it back to Admitted or Verified closes it (the
    /// listing simply drops off). No history is shown. Rules:
    /// - date = when the listing entered its current status: the first audit snapshot of the
    ///   latest unbroken run of that status (Scam → Questionable counts as entering Questionable);
    ///   without such a snapshot, the listing's UpdateDate (else CreateDate);
    /// - reason = the Note on that snapshot, so later note edits don't erase it; else the current note;
    /// - Removed listings are never included;
    /// - newest first.
    /// </summary>
    public static class OpenIssueBuilder
    {
        public static bool IsOpenStatus(DirectoryStatus status) =>
            status is DirectoryStatus.Scam or DirectoryStatus.Questionable;

        public static List<OpenIssue> Build(
            IEnumerable<AuditStatusRow> audits,
            IEnumerable<IncidentListingRow> listings)
        {
            var auditsByEntry = (audits ?? Enumerable.Empty<AuditStatusRow>())
                .GroupBy(a => a.DirectoryEntryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(a => a.EffectiveUtc).ThenBy(a => a.AuditId).ToList());

            var result = new List<OpenIssue>();
            foreach (var listing in (listings ?? Enumerable.Empty<IncidentListingRow>())
                .Where(l => IsOpenStatus(l.Status))
                .GroupBy(l => l.DirectoryEntryId)
                .Select(g => g.First()))
            {
                AuditStatusRow? start = null;
                if (auditsByEntry.TryGetValue(listing.DirectoryEntryId, out var rows))
                {
                    // Walk back over the trailing run of the current status.
                    for (int i = rows.Count - 1; i >= 0 && rows[i].Status == listing.Status; i--)
                    {
                        start = rows[i];
                    }
                }

                result.Add(new OpenIssue
                {
                    DirectoryEntryId = listing.DirectoryEntryId,
                    Name = listing.Name,
                    DirectoryEntryKey = listing.DirectoryEntryKey,
                    Status = listing.Status,
                    SinceUtc = start?.EffectiveUtc ?? listing.UpdateDate ?? listing.CreateDate,
                    Reason = Clean(start?.Note) ?? Clean(listing.Note),
                    Website = Clean(listing.Link),
                });
            }

            return result
                .OrderByDescending(i => i.SinceUtc)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string? Clean(string? note)
        {
            var s = note?.Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
