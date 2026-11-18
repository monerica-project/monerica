using DirectoryManager.Web.Controllers;

namespace DirectoryManager.Web.Tests.Submissions
{
    /// <summary>
    /// Submission 8525 (2026-11-18): an old submission still listed two tags that had since been
    /// deleted, and approving it failed half-way with a foreign-key error. Approval now links only
    /// tags that still exist.
    /// </summary>
    public class StaleTagIdsTests
    {
        [Fact]
        public void DeletedTagIdsAreDropped()
        {
            var kept = SubmissionController.KeepExistingTagIds(
                new[] { 3506, 3512, 3504, 3501, 3502, 3506 },
                new[] { 3504, 3506, 3512, 3600 });
            Assert.Equal(new[] { 3504, 3506, 3512 }, kept.OrderBy(x => x));
        }

        [Fact]
        public void NothingSelectedOrNothingLive_GivesAnEmptySet()
        {
            Assert.Empty(SubmissionController.KeepExistingTagIds(null, new[] { 1, 2 }));
            Assert.Empty(SubmissionController.KeepExistingTagIds(new[] { 1, 2 }, Array.Empty<int>()));
        }
    }
}
