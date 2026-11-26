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

        [Theory]
        [InlineData("(automated submission)", DirectoryStatus.Removed, "site offline (clearnet link offline)", SiteCheckFinding.MainSiteOffline)]
        [InlineData("(automated submission)", DirectoryStatus.Removed, "15% off | site offline (clearnet and tor link offline)", SiteCheckFinding.MainSiteOffline)]
        [InlineData("(automated submission)", DirectoryStatus.Removed, "site offline", SiteCheckFinding.MainSiteOffline)]
        [InlineData("(automated submission)", DirectoryStatus.Removed, "old note | site offline (tor link offline)", SiteCheckFinding.TorLinkOffline)]
        [InlineData("(automated submission) SSL certificate problem: expired.", DirectoryStatus.Verified, "a note", SiteCheckFinding.Warning)]
        [InlineData("(automated submission) Additional subcategory link offline: Misc: https://x.example.", DirectoryStatus.Verified, null, SiteCheckFinding.Warning)]
        [InlineData("please mark my site offline", DirectoryStatus.Removed, "site offline (clearnet link offline)", SiteCheckFinding.None)]
        [InlineData(null, DirectoryStatus.Removed, "site offline (clearnet link offline)", SiteCheckFinding.None)]
        public void Site_check_findings_are_sorted_by_what_is_down(string? noteToAdmin, DirectoryStatus status, string? note, SiteCheckFinding expected)
        {
            Assert.Equal(expected, AutomatedSubmissionRefresh.Classify(noteToAdmin, status, note));
        }

        [Theory]
        [InlineData("(automated submission)", null)]
        [InlineData("(automated submission) SSL certificate problem: expired.", "SSL certificate problem: expired.")]
        [InlineData("a person's note", null)]
        public void Warning_text_drops_the_marker(string noteToAdmin, string? expected)
        {
            Assert.Equal(expected, AutomatedSubmissionRefresh.WarningText(noteToAdmin));
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
