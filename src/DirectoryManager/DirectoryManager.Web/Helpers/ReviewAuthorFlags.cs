using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Author display for reviews shown outside their listing page (the /reviews and /incidents
    /// pages), with the same rule as the listing page: a review whose fingerprint matches the
    /// listing's own PGP key is the listing owner's (IsOwner, shown under the listing's name);
    /// every other review shows only its short fingerprint, never a self-chosen display name.
    /// Needs <see cref="DirectoryEntryReview.DirectoryEntry"/> loaded.
    /// </summary>
    public static class ReviewAuthorFlags
    {
        public static void ApplyOwnerFlags(IEnumerable<DirectoryEntryReview> reviews)
        {
            var fingerprintsByEntry = new Dictionary<int, List<string>>();

            foreach (var r in reviews ?? Enumerable.Empty<DirectoryEntryReview>())
            {
                var entry = r.DirectoryEntry;
                if (entry == null || string.IsNullOrWhiteSpace(entry.PgpKey))
                {
                    r.IsOwner = false;
                    r.DisplayName = null;
                    continue;
                }

                if (!fingerprintsByEntry.TryGetValue(entry.DirectoryEntryId, out var fps))
                {
                    fps = PgpFingerprintTools.GetAllFingerprints(entry.PgpKey).ToList();
                    fingerprintsByEntry[entry.DirectoryEntryId] = fps;
                }

                var reviewNorm = PgpFingerprintTools.Normalize(r.AuthorFingerprint);
                bool isOwner = fps.Any(fp => PgpFingerprintTools.Matches(reviewNorm, fp));
                r.IsOwner = isOwner;
                r.DisplayName = isOwner ? entry.Name : null;
            }
        }
    }
}
