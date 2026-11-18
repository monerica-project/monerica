using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class DeletedReviewArchiveTests
    {
        private const string AuthorFp = "usr:author";
        private const string ModeratorFp = "0175D6D85681BB958B36ADC1E4D98272B636CE45";

        [Fact]
        public async Task AuthorDeletesReview_TextIsNotKept_ButTheRecordAndOtherPeoplesRepliesAre()
        {
            using var db = NewContext();
            var id = await SeedAsync(db);

            await new DirectoryEntryReviewRepository(db).DeleteAsync(id, default, DeletedReviewRecord.ByAuthor);

            Assert.Empty(db.DirectoryEntryReviews);
            var record = Assert.Single(db.DeletedReviewRecords);
            Assert.Equal(DeletedReviewRecord.ByAuthor, record.DeletedBy);
            Assert.Equal(DeletedReviewRecord.KindReview, record.Kind);
            Assert.Equal(id, record.OriginalId);
            Assert.Equal("Entry 1", record.ListingName);
            Assert.Equal("tony", record.AuthorUsername);
            Assert.Equal((byte)1, record.Rating);
            Assert.Equal("Approved", record.StatusAtDelete);
            Assert.Equal(2, record.ReplyCount);

            // The author's own words are gone everywhere in the record.
            Assert.Equal(string.Empty, record.Body);
            Assert.DoesNotContain("got scammed", record.SnapshotJson);
            Assert.DoesNotContain("order/secret", record.SnapshotJson);
            Assert.DoesNotContain("my own follow-up", record.SnapshotJson);

            // The moderator's reply is kept; the author's own reply is not.
            var copy = DeletedReviewArchive.ReadReview(record)!;
            Assert.True(copy.ContentRemovedByAuthor);
            Assert.Equal("They are refunding you.", Assert.Single(copy.Replies).Body);

            // Nothing to restore from.
            Assert.Null(await DeletedReviewArchive.RestoreReviewAsync(db, record.Id, default));
            Assert.Empty(db.DirectoryEntryReviews);
        }

        [Fact]
        public async Task AdminDeletesReview_FullCopyKept_AndRestoreBringsItBackAsPending()
        {
            using var db = NewContext();
            var id = await SeedAsync(db);

            await new DirectoryEntryReviewRepository(db).DeleteAsync(id);

            var record = Assert.Single(db.DeletedReviewRecords);
            Assert.Equal(DeletedReviewRecord.ByAdmin, record.DeletedBy);
            Assert.Contains("got scammed", record.Body);

            var newId = await DeletedReviewArchive.RestoreReviewAsync(db, record.Id, default);
            Assert.NotNull(newId);
            var restored = await db.DirectoryEntryReviews.SingleAsync();
            Assert.Equal(ReviewModerationStatus.Pending, restored.ModerationStatus);
            Assert.Contains("got scammed", restored.Body);
            Assert.Equal("https://entry1.example/order/secret", restored.OrderUrl);
            Assert.Equal(2, await db.DirectoryEntryReviewComments.CountAsync(c => c.DirectoryEntryReviewId == newId));
            Assert.Equal(newId, (await db.DeletedReviewRecords.SingleAsync()).RestoredAsId);

            // A second restore does nothing.
            Assert.Null(await DeletedReviewArchive.RestoreReviewAsync(db, record.Id, default));
            Assert.Equal(1, await db.DirectoryEntryReviews.CountAsync());
        }

        [Fact]
        public async Task AuthorDeletesOwnReply_RecordKept_WithoutText()
        {
            using var db = NewContext();
            await SeedAsync(db);
            var replyId = await db.DirectoryEntryReviewComments.Where(c => c.AuthorFingerprint == AuthorFp).Select(c => c.DirectoryEntryReviewCommentId).SingleAsync();

            await new DirectoryEntryReviewCommentRepository(db).DeleteAsync(replyId, default, DeletedReviewRecord.ByAuthor);

            var record = Assert.Single(db.DeletedReviewRecords);
            Assert.Equal(DeletedReviewRecord.KindReply, record.Kind);
            Assert.Equal(replyId, record.OriginalId);
            Assert.Equal(string.Empty, record.Body);
            Assert.Null(record.SnapshotJson);
            Assert.Equal(1, await db.DirectoryEntryReviewComments.CountAsync());
        }

        private static async Task<int> SeedAsync(ApplicationDbContext db)
        {
            db.DirectoryEntries.Add(new DirectoryEntry
            {
                DirectoryEntryId = 1,
                Name = "Entry 1",
                DirectoryEntryKey = "entry-1",
                Link = "https://entry1.example",
                DirectoryStatus = DirectoryStatus.Verified,
                SubCategoryId = 1,
            });
            var review = new DirectoryEntryReview
            {
                DirectoryEntryId = 1,
                AuthorFingerprint = AuthorFp,
                AuthorUsername = "tony",
                Rating = 1,
                Body = "Tried to exchange and got scammed.",
                OrderUrl = "https://entry1.example/order/secret",
                ModerationStatus = ReviewModerationStatus.Approved,
                CreateDate = DateTime.UtcNow.AddHours(-9),
                CreatedByUserId = "system",
            };
            db.DirectoryEntryReviews.Add(review);
            await db.SaveChangesAsync();
            db.DirectoryEntryReviewComments.Add(new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = review.DirectoryEntryReviewId,
                Body = "They are refunding you.",
                AuthorFingerprint = ModeratorFp,
                ModerationStatus = ReviewModerationStatus.Approved,
                CreateDate = DateTime.UtcNow.AddHours(-2),
                CreatedByUserId = "system",
            });
            db.DirectoryEntryReviewComments.Add(new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = review.DirectoryEntryReviewId,
                Body = "my own follow-up",
                AuthorFingerprint = AuthorFp,
                AuthorUsername = "tony",
                ModerationStatus = ReviewModerationStatus.Approved,
                CreateDate = DateTime.UtcNow.AddHours(-1),
                CreatedByUserId = "system",
            });
            await db.SaveChangesAsync();
            return review.DirectoryEntryReviewId;
        }

        private static ApplicationDbContext NewContext() =>
            new (new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"deleted-{Guid.NewGuid():N}").Options);
    }
}
