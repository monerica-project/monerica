using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    // SiteChecker submissions are refreshed from the live listing at review time, so approving one
    // can't undo admin edits made after the check ran.
    public class AutomatedSubmissionRefreshTests
    {
        [Theory]
        [InlineData(true, 5, "(automated submission)", true)]
        [InlineData(true, 5, "(automated submission) SSL certificate problem: expired.", true)]
        [InlineData(false, 5, "(automated submission)", false)]                 // already handled
        [InlineData(true, null, "(automated submission)", false)]               // new-listing submission
        [InlineData(true, 5, "[auto-detail-check] KYC changed", false)]         // DetailChecker proposes real changes
        [InlineData(true, 5, "please update my listing", false)]                // a person's edit
        [InlineData(true, 5, null, false)]
        public void Only_pending_site_checker_submissions_are_refreshed(bool pending, int? entryId, string? noteToAdmin, bool expected)
        {
            Assert.Equal(expected, AutomatedSubmissionRefresh.ShouldRefresh(pending, entryId, noteToAdmin));
        }

        [Fact]
        public void Offline_keeps_removed_and_adds_reason_to_the_current_note()
        {
            var (status, note) = AutomatedSubmissionRefresh.ProposedStatusAndNote(
                DirectoryStatus.Removed,
                "old note | site offline (clearnet link offline)",
                DirectoryStatus.Verified,
                "note the admin rewrote");

            Assert.Equal(DirectoryStatus.Removed, status);
            Assert.Equal("note the admin rewrote | site offline (clearnet link offline)", note);
        }

        [Fact]
        public void Offline_with_no_current_note_uses_just_the_reason()
        {
            var (status, note) = AutomatedSubmissionRefresh.ProposedStatusAndNote(
                DirectoryStatus.Removed, "site offline (tor link offline)", DirectoryStatus.Admitted, null);

            Assert.Equal(DirectoryStatus.Removed, status);
            Assert.Equal("site offline (tor link offline)", note);
        }

        [Fact]
        public void Offline_reason_is_not_added_twice()
        {
            var (_, note) = AutomatedSubmissionRefresh.ProposedStatusAndNote(
                DirectoryStatus.Removed, "x | site offline", DirectoryStatus.Removed, "gone | site offline");

            Assert.Equal("gone | site offline", note);
        }

        [Fact]
        public void No_change_submissions_take_the_current_status_and_note()
        {
            // SSL / dead-link warnings propose no change: if the admin changed the listing since,
            // the proposal must follow the CURRENT listing, not the old copy.
            var (status, note) = AutomatedSubmissionRefresh.ProposedStatusAndNote(
                DirectoryStatus.Admitted, "old note", DirectoryStatus.Verified, "new note");

            Assert.Equal(DirectoryStatus.Verified, status);
            Assert.Equal("new note", note);
        }
    }
}
