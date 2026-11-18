using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    /// <summary>
    /// The public /reviews and /incidents queries: only approved reviews on listings that are not
    /// Removed, the filters, and which reviews count as problem reports.
    /// </summary>
    public class TrustPagesRepositoryTests
    {
        private static readonly DateTime Now = new (2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task ListPublic_ShowsOnlyApprovedReviews_OnListingsThatAreNotRemoved()
        {
            using var context = NewContext();
            Seed(context);
            var repo = new DirectoryEntryReviewRepository(context);

            var result = await repo.ListPublicAsync(new PublicReviewFilter());

            Assert.Equal(new[] { 105, 104, 103, 101 }, result.Items.Select(r => r.DirectoryEntryReviewId));
            Assert.Equal(4, result.TotalCount);
            Assert.DoesNotContain(result.Items, r => r.ModerationStatus != ReviewModerationStatus.Approved);
            Assert.DoesNotContain(result.Items, r => r.DirectoryEntry.DirectoryStatus == DirectoryStatus.Removed);
        }

        [Fact]
        public async Task ListPublic_FiltersByCategoryRatingTagAndReplies()
        {
            using var context = NewContext();
            Seed(context);
            var repo = new DirectoryEntryReviewRepository(context);

            var byCategory = await repo.ListPublicAsync(new PublicReviewFilter { CategoryId = 2 });
            Assert.Equal(new[] { 105 }, byCategory.Items.Select(r => r.DirectoryEntryReviewId));

            var byRating = await repo.ListPublicAsync(new PublicReviewFilter { Rating = 1 });
            Assert.Equal(new[] { 103 }, byRating.Items.Select(r => r.DirectoryEntryReviewId));

            var byTag = await repo.ListPublicAsync(new PublicReviewFilter { TagId = 15 });
            Assert.Equal(new[] { 103 }, byTag.Items.Select(r => r.DirectoryEntryReviewId));

            var withReplies = await repo.ListPublicAsync(new PublicReviewFilter { WithReplies = true });
            Assert.Equal(new[] { 101 }, withReplies.Items.Select(r => r.DirectoryEntryReviewId));
        }

        [Fact]
        public async Task ListPublic_SortsAndClampsThePage()
        {
            using var context = NewContext();
            Seed(context);
            var repo = new DirectoryEntryReviewRepository(context);

            var oldest = await repo.ListPublicAsync(new PublicReviewFilter { Sort = PublicReviewSort.Oldest });
            Assert.Equal(new[] { 101, 103, 104, 105 }, oldest.Items.Select(r => r.DirectoryEntryReviewId));

            var highest = await repo.ListPublicAsync(new PublicReviewFilter { Sort = PublicReviewSort.Highest });
            Assert.Equal((byte?)5, highest.Items[0].Rating);
            Assert.Null(highest.Items[^1].Rating); // unrated last

            var lowest = await repo.ListPublicAsync(new PublicReviewFilter { Sort = PublicReviewSort.Lowest });
            Assert.Equal((byte?)1, lowest.Items[0].Rating);
            Assert.Null(lowest.Items[^1].Rating);

            var farPage = await repo.ListPublicAsync(new PublicReviewFilter { Page = 999 });
            Assert.Equal(1, farPage.Page);
            Assert.Equal(4, farPage.Items.Count);
        }

        [Fact]
        public async Task ProblemReports_AreApprovedReviewsWithAPublicWarningLevelTag()
        {
            using var context = NewContext();
            Seed(context);
            var repo = new DirectoryEntryReviewRepository(context);

            var result = await repo.ListProblemReportsAsync(null, 1, 20);

            // 103 = Funds held (public, Warning). Not: 104 (money band only), 105 (admin-only Suspicious),
            // 102 (pending), 106 (listing Removed), 107 (rejected with a problem tag).
            Assert.Equal(new[] { 103 }, result.Items.Select(r => r.DirectoryEntryReviewId));

            var recent = await repo.ListProblemReportsAsync(Now.AddDays(-5), 1, 20);
            Assert.Empty(recent.Items); // 103 is 8 days old
        }

        [Fact]
        public async Task ReplyCounts_CountApprovedRepliesOnly()
        {
            using var context = NewContext();
            Seed(context);
            var repo = new DirectoryEntryReviewRepository(context);

            var counts = await repo.GetApprovedReplyCountsAsync(new[] { 101, 103 });

            Assert.Equal(1, counts[101]);
            Assert.False(counts.ContainsKey(103));
        }

        [Fact]
        public async Task ReviewPageMap_PinsOfficialReviewsToPageOne_AndDoesNotCountThem()
        {
            using var context = NewContext();
            Seed(context);
            var official = Review(108, 1, ReviewModerationStatus.Approved, 5, Now.AddDays(-1));
            official.IsOfficial = true;
            context.DirectoryEntryReviews.Add(official);
            await context.SaveChangesAsync();
            var repo = new DirectoryEntryReviewRepository(context);

            // Crowd order on entry 1 (newest first): 104, 103, 101 → with 2 per page 103 is on page 1
            // (it would be page 2 if the newer official review were counted) and 101 on page 2.
            var map = await repo.GetApprovedReviewPageMapAsync(new[] { 101, 103, 104, 108 }, pageSize: 2);

            Assert.Equal(1, map[108]);
            Assert.Equal(1, map[104]);
            Assert.Equal(1, map[103]);
            Assert.Equal(2, map[101]);
        }

        [Fact]
        public async Task OpenIssueHistory_CoversOnlyListingsThatAreScamOrQuestionableNow()
        {
            using var context = NewContext();
            Seed(context);
            context.DirectoryEntries.Add(Entry(4, "flagged", DirectoryStatus.Questionable, 10));
            context.DirectoryEntriesAudit.AddRange(
                Audit(1, 1, DirectoryStatus.Scam, Now.AddDays(-30), "old issue"),      // entry 1 is Verified now: closed
                Audit(2, 1, DirectoryStatus.Verified, Now.AddDays(-3)),
                Audit(3, 3, DirectoryStatus.Questionable, Now.AddDays(-9)),            // entry 3 is Removed now
                Audit(4, 4, DirectoryStatus.Admitted, Now.AddDays(-50)),
                Audit(5, 4, DirectoryStatus.Questionable, Now.AddDays(-2), "reseller on a listed network"));
            await context.SaveChangesAsync();
            var repo = new DirectoryEntriesAuditRepository(context);

            var (audits, listings) = await repo.GetOpenIssueHistoryAsync();

            Assert.Equal(new[] { 4 }, listings.Select(l => l.DirectoryEntryId));
            Assert.All(audits, a => Assert.Equal(4, a.DirectoryEntryId));
            Assert.Equal(2, audits.Count);
            Assert.Equal(Now.AddDays(-400), listings[0].CreateDate);
        }

        private static DirectoryEntriesAudit Audit(int id, int entryId, DirectoryStatus status, DateTime when, string? note = null) => new ()
        {
            DirectoryEntriesAuditId = id,
            DirectoryEntryId = entryId,
            DirectoryStatus = status,
            CreateDate = when,
            UpdateDate = when,
            Note = note,
            Name = "x",
            Link = "https://x.example",
        };

        private static void Seed(ApplicationDbContext context)
        {
            context.Categories.AddRange(
                new Category { CategoryId = 1, Name = "Exchanges", CategoryKey = "exchanges" },
                new Category { CategoryId = 2, Name = "Shops", CategoryKey = "shops" });
            context.Subcategories.AddRange(
                new Subcategory { SubCategoryId = 10, CategoryId = 1, Name = "Swaps", SubCategoryKey = "swaps" },
                new Subcategory { SubCategoryId = 20, CategoryId = 2, Name = "Food", SubCategoryKey = "food" });
            context.DirectoryEntries.AddRange(
                Entry(1, "alpha", DirectoryStatus.Verified, 10),
                Entry(2, "beta", DirectoryStatus.Admitted, 20),
                Entry(3, "gone", DirectoryStatus.Removed, 10));
            context.ReviewTags.AddRange(
                new ReviewTag { ReviewTagId = 2, Name = "Under $50", Slug = "under-50", Level = ReviewTagLevel.Info, MaxUsd = 50 },
                new ReviewTag { ReviewTagId = 6, Name = "Suspicious Review", Slug = "suspicious", Level = ReviewTagLevel.Warning, ReviewerSelectable = false },
                new ReviewTag { ReviewTagId = 15, Name = "Funds held", Slug = "funds-held", Level = ReviewTagLevel.Warning });
            context.DirectoryEntryReviews.AddRange(
                Review(101, 1, ReviewModerationStatus.Approved, 4, Now.AddDays(-10)),
                Review(102, 1, ReviewModerationStatus.Pending, 2, Now.AddDays(-9)),
                Review(103, 1, ReviewModerationStatus.Approved, 1, Now.AddDays(-8)),
                Review(104, 1, ReviewModerationStatus.Approved, 5, Now.AddDays(-7)),
                Review(105, 2, ReviewModerationStatus.Approved, null, Now.AddDays(-6)),
                Review(106, 3, ReviewModerationStatus.Approved, 1, Now.AddDays(-5)),
                Review(107, 1, ReviewModerationStatus.Rejected, 1, Now.AddDays(-4)));
            context.DirectoryEntryReviewTags.AddRange(
                new DirectoryEntryReviewTag { DirectoryEntryReviewId = 102, ReviewTagId = 15 },
                new DirectoryEntryReviewTag { DirectoryEntryReviewId = 103, ReviewTagId = 15 },
                new DirectoryEntryReviewTag { DirectoryEntryReviewId = 104, ReviewTagId = 2 },
                new DirectoryEntryReviewTag { DirectoryEntryReviewId = 105, ReviewTagId = 6 },
                new DirectoryEntryReviewTag { DirectoryEntryReviewId = 106, ReviewTagId = 15 },
                new DirectoryEntryReviewTag { DirectoryEntryReviewId = 107, ReviewTagId = 15 });
            context.DirectoryEntryReviewComments.AddRange(
                new DirectoryEntryReviewComment { DirectoryEntryReviewCommentId = 1, DirectoryEntryReviewId = 101, Body = "thanks", ModerationStatus = ReviewModerationStatus.Approved, AuthorFingerprint = "A" },
                new DirectoryEntryReviewComment { DirectoryEntryReviewCommentId = 2, DirectoryEntryReviewId = 103, Body = "pending", ModerationStatus = ReviewModerationStatus.Pending, AuthorFingerprint = "B" });
            context.SaveChanges();
        }

        private static DirectoryEntry Entry(int id, string key, DirectoryStatus status, int subId) => new ()
        {
            DirectoryEntryId = id,
            DirectoryEntryKey = key,
            Name = key,
            Link = $"https://{key}.example",
            DirectoryStatus = status,
            SubCategoryId = subId,
            CreateDate = Now.AddDays(-400),
        };

        private static DirectoryEntryReview Review(int id, int entryId, ReviewModerationStatus status, byte? rating, DateTime when) => new ()
        {
            DirectoryEntryReviewId = id,
            DirectoryEntryId = entryId,
            ModerationStatus = status,
            Rating = rating,
            Body = $"review {id}",
            AuthorFingerprint = "ABCDEF0123456789ABCDEF0123456789ABCDEF01",
            CreateDate = when,
        };

        private static ApplicationDbContext NewContext() =>
            new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"trust-pages-{Guid.NewGuid():N}").Options);
    }
}
