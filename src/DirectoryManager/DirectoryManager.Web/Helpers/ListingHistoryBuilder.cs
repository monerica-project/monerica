using DirectoryManager.Data.Models;
using DirectoryManager.Web.Models.ListingHistory;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Turns a set of full audit snapshots into a minimal, newest-first change
    /// timeline. Only fields that actually changed between consecutive snapshots
    /// are emitted; snapshots with no tracked changes are dropped entirely.
    /// User-id columns are intentionally never exposed.
    /// </summary>
    public static class ListingHistoryBuilder
    {
        public static ListingHistoryViewModel Build(
            int directoryEntryId,
            string entryName,
            string? listingUrl,
            IEnumerable<DirectoryEntriesAudit> audits,
            string link2Label,
            string link3Label)
        {
            var ordered = (audits ?? Enumerable.Empty<DirectoryEntriesAudit>())
                .OrderBy(a => a.UpdateDate ?? a.CreateDate)
                .ThenBy(a => a.DirectoryEntriesAuditId)
                .ToList();

            var revisions = new List<ListingHistoryRevision>();

            // Ordered, public-safe field set. Label + value selector.
            var fields = BuildFieldDefs(link2Label, link3Label);

            for (int i = 0; i < ordered.Count; i++)
            {
                var curr = ordered[i];
                var effective = curr.UpdateDate ?? curr.CreateDate;

                if (i == 0)
                {
                    // First snapshot = the listing being added.
                    revisions.Add(new ListingHistoryRevision
                    {
                        TimestampUtc = effective,
                        IsCreation = true,
                        Changes = new List<ListingFieldChange>()
                    });
                    continue;
                }

                var prev = ordered[i - 1];
                var changes = new List<ListingFieldChange>();

                foreach (var (label, selector) in fields)
                {
                    var oldVal = Normalize(selector(prev));
                    var newVal = Normalize(selector(curr));

                    // Casing-only edits are cosmetic and should not appear in the log.
                    if (!string.Equals(oldVal, newVal, StringComparison.OrdinalIgnoreCase))
                    {
                        changes.Add(new ListingFieldChange
                        {
                            Field = label,
                            OldValue = string.IsNullOrEmpty(oldVal) ? null : oldVal,
                            NewValue = string.IsNullOrEmpty(newVal) ? null : newVal
                        });
                    }
                }

                // Skip revisions where nothing we track changed.
                if (changes.Count == 0)
                {
                    continue;
                }

                revisions.Add(new ListingHistoryRevision
                {
                    TimestampUtc = effective,
                    IsCreation = false,
                    Changes = changes
                });
            }

            // Newest first for display.
            revisions.Reverse();

            return new ListingHistoryViewModel
            {
                DirectoryEntryId = directoryEntryId,
                Name = entryName,
                ListingUrl = listingUrl,
                Revisions = revisions
            };
        }

        private static List<(string Label, Func<DirectoryEntriesAudit, string?> Selector)> BuildFieldDefs(
            string link2Label,
            string link3Label)
        {
            var l2 = string.IsNullOrWhiteSpace(link2Label) ? "Link 2" : link2Label.Trim();
            var l3 = string.IsNullOrWhiteSpace(link3Label) ? "Link 3" : link3Label.Trim();

            return new List<(string, Func<DirectoryEntriesAudit, string?>)>
            {
                ("Status",      a => a.DirectoryStatus.ToString()),
                ("Name",        a => a.Name),
                ("Link",        a => a.Link),
                (l2,            a => a.Link2),
                (l3,            a => a.Link3),
                ("Subcategory", a => SubcategoryDisplay(a)),
                ("Location",    a => a.Location),
                ("Processor",   a => a.Processor),
                ("Contact",     a => a.Contact),
                ("Description", a => a.Description),
                ("Note",        a => a.Note),
            };
        }

        private static string? SubcategoryDisplay(DirectoryEntriesAudit a)
        {
            if (a.SubCategory == null)
            {
                return null;
            }

            var category = a.SubCategory.Category?.Name;
            return string.IsNullOrWhiteSpace(category)
                ? a.SubCategory.Name
                : $"{category} > {a.SubCategory.Name}";
        }

        private static string Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}