using System;
using System.Linq;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Tests.Helpers
{
    /// <summary>
    /// Guards the DirectoryEntry -> Submission content copy used by the weekly SiteChecker (and any
    /// other path that mirrors a live entry into a submission). These tests are reflection-driven, so
    /// a NEW listing field added to both models is covered automatically — which is the whole point:
    /// a forgotten field can no longer be silently dropped and then "removed" on approval.
    /// </summary>
    public class SubmissionContentMapperTests
    {
        // Regression: this is the exact field the SiteChecker forgot, which made offline submissions
        // look like they were clearing a listing's source-code link.
        [Fact]
        public void CopyContentFields_CarriesSourceCodeLink()
        {
            var entry = NewEntry();
            entry.SourceCodeLink = "https://github.com/example/repo";

            var submission = new Submission();
            SubmissionContentMapper.CopyContentFields(entry, submission);

            Assert.Equal(entry.SourceCodeLink, submission.SourceCodeLink);
        }

        // The real safety net: give every mapped field a distinct value on the entry and assert the
        // mapper carries each one. Driven by MappedFieldNames, so any field added in the future is
        // exercised here with no test change.
        [Fact]
        public void CopyContentFields_CarriesEveryMappedContentField()
        {
            var entry = NewEntry();
            foreach (var name in SubmissionContentMapper.MappedFieldNames)
            {
                var prop = typeof(DirectoryEntry).GetProperty(name);
                Assert.NotNull(prop);
                prop.SetValue(entry, SampleValue(prop.PropertyType, name));
            }

            var submission = new Submission();
            SubmissionContentMapper.CopyContentFields(entry, submission);

            foreach (var name in SubmissionContentMapper.MappedFieldNames)
            {
                var entryProp = typeof(DirectoryEntry).GetProperty(name);
                var submissionProp = typeof(Submission).GetProperty(name);
                Assert.NotNull(entryProp);
                Assert.NotNull(submissionProp);

                var expected = entryProp.GetValue(entry);
                var actual = submissionProp.GetValue(submission);
                Assert.True(Equals(expected, actual), $"Field '{name}' was not carried to the submission.");
            }
        }

        // Caller-owned fields (status/note/tags/entry id) must be left exactly as the caller set them.
        [Fact]
        public void CopyContentFields_DoesNotTouchCallerOwnedFields()
        {
            var entry = NewEntry();
            entry.Note = "entry note";
            entry.DirectoryStatus = DirectoryStatus.Verified;

            var submission = new Submission
            {
                Note = "caller note",
                DirectoryStatus = DirectoryStatus.Removed,
                DirectoryEntryId = 42,
                Tags = "caller-tags",
            };

            SubmissionContentMapper.CopyContentFields(entry, submission);

            Assert.Equal("caller note", submission.Note);
            Assert.Equal(DirectoryStatus.Removed, submission.DirectoryStatus);
            Assert.Equal(42, submission.DirectoryEntryId);
            Assert.Equal("caller-tags", submission.Tags);
        }

        [Fact]
        public void MappedFieldNames_ExcludesCallerOwnedFields()
        {
            foreach (var excluded in SubmissionContentMapper.ExcludedFieldNames)
            {
                Assert.DoesNotContain(excluded, SubmissionContentMapper.MappedFieldNames);
            }
        }

        private static DirectoryEntry NewEntry() => new DirectoryEntry
        {
            Name = "Test",
            DirectoryEntryKey = Guid.NewGuid().ToString(),
            Link = "https://example.com",
        };

        private static object SampleValue(Type type, string name)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            if (t == typeof(string))
            {
                return "val-" + name;
            }

            if (t == typeof(int))
            {
                return 7;
            }

            if (t == typeof(bool))
            {
                return true;
            }

            if (t == typeof(decimal))
            {
                return 1.5m;
            }

            if (t == typeof(Guid))
            {
                return Guid.NewGuid();
            }

            if (t == typeof(DateTime))
            {
                return new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            }

            if (t == typeof(DateOnly))
            {
                return new DateOnly(2020, 1, 2);
            }

            if (t.IsEnum)
            {
                return Enum.GetValues(t).Cast<object>().First(v => Convert.ToInt64(v) != 0);
            }

            throw new NotSupportedException($"No sample value for {t} ({name}).");
        }
    }
}
