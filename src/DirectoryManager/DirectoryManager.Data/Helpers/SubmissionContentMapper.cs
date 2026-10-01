using System.Reflection;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Single source of truth for copying the shared, user-editable "content" fields from a live
    /// <see cref="DirectoryEntry"/> onto a <see cref="Submission"/>.
    ///
    /// <para>
    /// Why this exists: several paths build a Submission from an existing DirectoryEntry — most
    /// importantly the unattended weekly SiteChecker, which queues an "offline" submission proposing
    /// removal. Every such submission must carry ALL of the entry's current content forward, or
    /// approving it silently blanks whatever field was forgotten (and the admin diff shows that
    /// field being "removed"). That is exactly what happened when <c>SourceCodeLink</c> was added to
    /// the models but not to the SiteChecker's hand-written copy.
    /// </para>
    ///
    /// <para>
    /// To make that class of bug impossible going forward, the copy is driven by reflection: every
    /// scalar property that exists on BOTH types by the same name and a compatible type is carried
    /// automatically, so a new listing field added to both models flows through here with no code
    /// change. The few shared fields the CALLER must own (status, note, tags, the entry id) are
    /// listed in <see cref="ExcludedFieldNames"/> and are skipped here.
    /// </para>
    /// </summary>
    public static class SubmissionContentMapper
    {
        /// <summary>
        /// Properties that exist on both types by name but must NOT be auto-copied, because the
        /// caller sets them with context-specific values (or they are identity/relationship columns
        /// handled separately).
        /// </summary>
        public static readonly IReadOnlySet<string> ExcludedFieldNames = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Submission.DirectoryStatus), // caller decides (e.g. Removed for an offline site)
            nameof(Submission.Note),            // caller keeps/extends the existing note (offline reason)
            nameof(Submission.Tags),            // pulled from the DirectoryEntryTag join table, not the row
            nameof(Submission.DirectoryEntryId), // set explicitly so the submission targets the right entry
        };

        private static readonly (PropertyInfo Entry, PropertyInfo Submission)[] FieldPairs = BuildFieldPairs();

        /// <summary>The Submission property names this mapper copies (for tests / diagnostics).</summary>
        public static IReadOnlyCollection<string> MappedFieldNames { get; } =
            FieldPairs.Select(p => p.Submission.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Copies every shared content field from <paramref name="entry"/> onto
        /// <paramref name="submission"/>. Does not touch the <see cref="ExcludedFieldNames"/> — the
        /// caller owns those.
        /// </summary>
        public static void CopyContentFields(DirectoryEntry entry, Submission submission)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(submission);

            foreach (var (entryProp, submissionProp) in FieldPairs)
            {
                submissionProp.SetValue(submission, entryProp.GetValue(entry));
            }
        }

        private static (PropertyInfo, PropertyInfo)[] BuildFieldPairs()
        {
            var entryProps = typeof(DirectoryEntry)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, StringComparer.Ordinal);

            var pairs = new List<(PropertyInfo, PropertyInfo)>();

            foreach (var subProp in typeof(Submission).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!subProp.CanWrite || ExcludedFieldNames.Contains(subProp.Name))
                {
                    continue;
                }

                if (!entryProps.TryGetValue(subProp.Name, out var entryProp))
                {
                    continue;
                }

                if (!IsSimpleType(subProp.PropertyType) || !IsSimpleType(entryProp.PropertyType))
                {
                    continue; // skip navigation properties, collections, complex types
                }

                // Same underlying type, ignoring nullability (so int -> int? is fine).
                var subUnderlying = Nullable.GetUnderlyingType(subProp.PropertyType) ?? subProp.PropertyType;
                var entryUnderlying = Nullable.GetUnderlyingType(entryProp.PropertyType) ?? entryProp.PropertyType;
                if (subUnderlying != entryUnderlying)
                {
                    continue;
                }

                // Never copy a possibly-null source into a non-nullable value-type target — a null
                // would throw at assignment time.
                var entryIsNullable = !entryProp.PropertyType.IsValueType
                    || Nullable.GetUnderlyingType(entryProp.PropertyType) != null;
                var subIsNonNullableValue = subProp.PropertyType.IsValueType
                    && Nullable.GetUnderlyingType(subProp.PropertyType) == null;
                if (entryIsNullable && subIsNonNullableValue)
                {
                    continue;
                }

                pairs.Add((entryProp, subProp));
            }

            return pairs.ToArray();
        }

        private static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateOnly)
                || type == typeof(Guid);
        }
    }
}
