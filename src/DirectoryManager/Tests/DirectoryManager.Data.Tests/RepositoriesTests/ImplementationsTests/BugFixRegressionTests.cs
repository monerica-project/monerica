using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    // Regression tests for the 2026-10 bug-fix pass (sponsor live rule, paging math, reply
    // deletion with children, "my reviews" visibility, key collisions, site-checker dedupe).
    public class BugFixRegressionTests
    {
        // ---------------- live rule ----------------
        [Theory]
        [InlineData(DirectoryStatus.Admitted, true)]
        [InlineData(DirectoryStatus.Verified, true)]
        [InlineData(DirectoryStatus.Removed, false)]
        [InlineData(DirectoryStatus.Scam, false)]
        [InlineData(DirectoryStatus.Questionable, false)]
        [InlineData(DirectoryStatus.Unknown, false)]
        public void IsLive_OnlyAdmittedAndVerified(DirectoryStatus status, bool expected)
        {
            Assert.Equal(expected, status.IsLive());
        }

        [Fact]
        public async Task ActiveSponsorQueries_ExcludeRemovedAndScamListings()
        {
            using var context = NewContext();
            SeedSponsor(context, 1, DirectoryStatus.Verified, SponsorshipType.MainSponsor);
            SeedSponsor(context, 2, DirectoryStatus.Removed, SponsorshipType.MainSponsor);
            SeedSponsor(context, 3, DirectoryStatus.Scam, SponsorshipType.SubcategorySponsor);
            SeedSponsor(context, 4, DirectoryStatus.Admitted, SponsorshipType.SubcategorySponsor);
            await context.SaveChangesAsync();

            var repo = new SponsoredListingRepository(context);

            var main = await repo.GetActiveSponsorsByTypeAsync(SponsorshipType.MainSponsor);
            Assert.Equal(new[] { 1 }, main.Select(x => x.DirectoryEntryId));

            var all = await repo.GetAllActiveSponsorsAsync();
            Assert.Equal(new[] { 1, 4 }, all.Select(x => x.DirectoryEntryId).OrderBy(x => x));

            var sub = await repo.GetSponsoredListingsForSubCategory(1);
            Assert.Equal(new[] { 4 }, sub.Select(x => x.DirectoryEntryId));

            var ticker = await repo.GetSponsorTickerItemsAsync();
            Assert.Equal(new[] { 1, 4 }, ticker.Select(x => x.DirectoryEntryId).OrderBy(x => x));
        }

        [Fact]
        public async Task FeaturedSelection_ExcludesNonLiveListings()
        {
            using var context = NewContext();
            context.Categories.Add(new Category { CategoryId = 1, Name = "Cat", CategoryKey = "cat" });
            context.Subcategories.Add(new Subcategory { SubCategoryId = 1, CategoryId = 1, Name = "Sub", SubCategoryKey = "sub" });
            context.DirectoryEntries.Add(Entry(1, DirectoryStatus.Verified));
            context.DirectoryEntries.Add(Entry(2, DirectoryStatus.Removed));
            context.DirectoryEntrySelections.Add(new DirectoryEntrySelection { DirectoryEntryId = 1, EntrySelectionType = EntrySelectionType.Featured });
            context.DirectoryEntrySelections.Add(new DirectoryEntrySelection { DirectoryEntryId = 2, EntrySelectionType = EntrySelectionType.Featured });
            await context.SaveChangesAsync();

            var featured = await new DirectoryEntrySelectionRepository(context).GetEntriesForSelection(EntrySelectionType.Featured);

            Assert.Equal(new[] { 1 }, featured.Select(e => e.DirectoryEntryId));
        }

        // ---------------- paging ----------------
        [Theory]
        [InlineData(-5, 10, 0)]
        [InlineData(0, 10, 0)]
        [InlineData(1, 10, 0)]
        [InlineData(3, 10, 20)]
        [InlineData(2, -10, 0)]
        [InlineData(int.MaxValue, 100, int.MaxValue)]
        public void Skip_NeverNegativeAndNeverOverflows(int page, int pageSize, int expected)
        {
            Assert.Equal(expected, PagingHelper.Skip(page, pageSize));
        }

        [Theory]
        [InlineData(0, 10, 1)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(5, 0, 1)]
        [InlineData(int.MaxValue, 1, int.MaxValue)]
        public void TotalPages_IsAtLeastOne(int total, int pageSize, int expected)
        {
            Assert.Equal(expected, PagingHelper.TotalPages(total, pageSize));
        }

        [Theory]
        [InlineData(-1, 25, 25)]
        [InlineData(0, 25, 25)]
        [InlineData(5, 25, 5)]
        [InlineData(100000, 25, PagingHelper.MaxPublicPageSize)]
        public void ClampPageSize_StaysInRange(int requested, int fallback, int expected)
        {
            Assert.Equal(expected, PagingHelper.ClampPageSize(requested, fallback));
        }

        // ---------------- review replies ----------------
        [Fact]
        public async Task DeleteOwnedReply_WithChildren_ReparentsChildrenInsteadOfFailing()
        {
            using var context = NewContext();
            SeedReview(context, 1);
            context.DirectoryEntryReviewComments.AddRange(
                Comment(10, 1, null, "AAA", ReviewModerationStatus.Approved),
                Comment(11, 1, 10, "BBB", ReviewModerationStatus.Approved),
                Comment(12, 1, 11, "CCC", ReviewModerationStatus.Approved));
            await context.SaveChangesAsync();

            var repo = new DirectoryEntryReviewCommentRepository(context);

            Assert.True(await repo.DeleteOwnedAsync(11, "BBB"));

            Assert.Null(await context.DirectoryEntryReviewComments.FindAsync(11));
            var grandchild = await context.DirectoryEntryReviewComments.AsNoTracking().SingleAsync(c => c.DirectoryEntryReviewCommentId == 12);
            Assert.Equal(10, grandchild.ParentCommentId);
        }

        [Fact]
        public async Task AdminDeleteReply_WithChildren_MovesChildrenToTopLevel()
        {
            using var context = NewContext();
            SeedReview(context, 1);
            context.DirectoryEntryReviewComments.AddRange(
                Comment(10, 1, null, "AAA", ReviewModerationStatus.Approved),
                Comment(11, 1, 10, "BBB", ReviewModerationStatus.Approved));
            await context.SaveChangesAsync();

            await new DirectoryEntryReviewCommentRepository(context).DeleteAsync(10);

            var child = await context.DirectoryEntryReviewComments.AsNoTracking().SingleAsync();
            Assert.Equal(11, child.DirectoryEntryReviewCommentId);
            Assert.Null(child.ParentCommentId);
        }

        [Fact]
        public async Task DeleteReview_WithNestedReplies_RemovesReviewAndReplies()
        {
            using var context = NewContext();
            SeedReview(context, 1);
            context.DirectoryEntryReviewComments.AddRange(
                Comment(10, 1, null, "AAA", ReviewModerationStatus.Approved),
                Comment(11, 1, 10, "BBB", ReviewModerationStatus.Approved));
            await context.SaveChangesAsync();

            await new DirectoryEntryReviewRepository(context).DeleteAsync(1);

            Assert.Empty(await context.DirectoryEntryReviews.AsNoTracking().ToListAsync());
            Assert.Empty(await context.DirectoryEntryReviewComments.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task MyReviewsThread_ShowsApprovedPlusOnlyTheUsersOwnUnapprovedReplies()
        {
            using var context = NewContext();
            SeedReview(context, 1);
            context.DirectoryEntryReviewComments.AddRange(
                Comment(10, 1, null, "OTHER", ReviewModerationStatus.Approved),
                Comment(11, 1, null, "OTHER", ReviewModerationStatus.Pending),
                Comment(12, 1, null, "OTHER", ReviewModerationStatus.Rejected),
                Comment(13, 1, null, "ME", ReviewModerationStatus.Pending),
                Comment(14, 1, null, "ME", ReviewModerationStatus.Rejected));
            await context.SaveChangesAsync();

            var visible = await new DirectoryEntryReviewCommentRepository(context)
                .ListForReviewVisibleToAuthorAsync(1, "ME");

            Assert.Equal(new[] { 10, 13, 14 }, visible.Select(c => c.DirectoryEntryReviewCommentId).OrderBy(x => x));
        }

        // ---------------- key collisions ----------------
        [Fact]
        public async Task CategoryAndSubcategoryKeyExists_ExcludesSelf()
        {
            using var context = NewContext();
            context.Categories.Add(new Category { CategoryId = 1, Name = "A", CategoryKey = "a" });
            context.Categories.Add(new Category { CategoryId = 2, Name = "B", CategoryKey = "b" });
            context.Subcategories.Add(new Subcategory { SubCategoryId = 1, CategoryId = 1, Name = "X", SubCategoryKey = "x" });
            await context.SaveChangesAsync();

            var cats = new CategoryRepository(context);
            Assert.True(await cats.KeyExistsAsync("a"));
            Assert.False(await cats.KeyExistsAsync("a", excludeCategoryId: 1));
            Assert.True(await cats.KeyExistsAsync("a", excludeCategoryId: 2));

            var subs = new SubcategoryRepository(context);
            Assert.True(await subs.KeyExistsAsync(1, "x"));
            Assert.False(await subs.KeyExistsAsync(1, "x", excludeSubCategoryId: 1));
            Assert.False(await subs.KeyExistsAsync(2, "x"));
        }

        // ---------------- site checker dedupe ----------------
        [Fact]
        public async Task HasPendingWithNoteContaining_MatchesOnlyPendingOfflineProposals()
        {
            using var context = NewContext();
            context.Submissions.Add(new Submission { SubmissionStatus = SubmissionStatus.Pending, DirectoryEntryId = 5, Link = "https://a.example", Note = "user edit" });
            context.Submissions.Add(new Submission { SubmissionStatus = SubmissionStatus.Rejected, DirectoryEntryId = 6, Link = "https://b.example", Note = "site offline (clearnet link offline)" });
            context.Submissions.Add(new Submission { SubmissionStatus = SubmissionStatus.Pending, DirectoryEntryId = 7, Link = "https://c.example", Note = "x | site offline (tor link offline)" });
            await context.SaveChangesAsync();

            var repo = new SubmissionRepository(context);

            Assert.False(await repo.HasPendingWithNoteContainingAsync(5, "https://a.example", "site offline"));
            Assert.False(await repo.HasPendingWithNoteContainingAsync(6, "https://b.example", "site offline"));
            Assert.True(await repo.HasPendingWithNoteContainingAsync(7, "https://c.example", "site offline"));
            Assert.True(await repo.HasPendingWithNoteContainingAsync(0, "https://c.example", "site offline"));
        }

        private static ApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"bugfix-{Guid.NewGuid():N}")
                .Options;
            return new ApplicationDbContext(options);
        }

        private static DirectoryEntry Entry(int id, DirectoryStatus status) => new DirectoryEntry
        {
            DirectoryEntryId = id,
            Name = $"Entry {id}",
            DirectoryEntryKey = $"entry-{id}",
            Link = $"https://entry{id}.example",
            DirectoryStatus = status,
            SubCategoryId = 1,
        };

        private static void SeedSponsor(ApplicationDbContext context, int entryId, DirectoryStatus status, SponsorshipType type)
        {
            if (!context.Categories.Local.Any())
            {
                var category = new Category { CategoryId = 1, Name = "Cat", CategoryKey = "cat" };
                context.Categories.Add(category);
                context.Subcategories.Add(new Subcategory { SubCategoryId = 1, CategoryId = 1, Name = "Sub", SubCategoryKey = "sub" });
            }

            context.DirectoryEntries.Add(Entry(entryId, status));
            context.SponsoredListings.Add(new SponsoredListing
            {
                SponsoredListingId = entryId,
                DirectoryEntryId = entryId,
                SponsoredListingInvoiceId = entryId,
                SponsorshipType = type,
                SubCategoryId = type == SponsorshipType.SubcategorySponsor ? 1 : null,
                CampaignStartDate = DateTime.UtcNow.AddDays(-1),
                CampaignEndDate = DateTime.UtcNow.AddDays(10),
            });
        }

        private static void SeedReview(ApplicationDbContext context, int reviewId)
        {
            context.DirectoryEntries.Add(Entry(1, DirectoryStatus.Verified));
            context.DirectoryEntryReviews.Add(new DirectoryEntryReview
            {
                DirectoryEntryReviewId = reviewId,
                DirectoryEntryId = 1,
                AuthorFingerprint = "REVIEWER",
                Body = "review",
                ModerationStatus = ReviewModerationStatus.Approved,
            });
        }

        private static DirectoryEntryReviewComment Comment(int id, int reviewId, int? parentId, string fingerprint, ReviewModerationStatus status) =>
            new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewCommentId = id,
                DirectoryEntryReviewId = reviewId,
                ParentCommentId = parentId,
                AuthorFingerprint = fingerprint,
                Body = $"reply {id}",
                ModerationStatus = status,
                CreateDate = DateTime.UtcNow.AddMinutes(id),
            };
    }
}
