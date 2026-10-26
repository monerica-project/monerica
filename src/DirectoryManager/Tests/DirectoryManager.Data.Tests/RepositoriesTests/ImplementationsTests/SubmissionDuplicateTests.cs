using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    // A link (or an edit of a listing) that is already waiting for review can't be sent again.
    public class SubmissionDuplicateTests
    {
        [Fact]
        public async Task SameLinkTypedDifferently_IsADuplicate()
        {
            using var context = Seeded();
            var repo = new SubmissionRepository(context);

            var dup = await repo.FindPendingDuplicateAsync("http://www.Shop.example.com/", null, null);

            Assert.Equal(1, dup?.SubmissionId);
        }

        [Fact]
        public async Task RejectedOrDraftSubmissions_DoNotBlock()
        {
            using var context = Seeded();
            var repo = new SubmissionRepository(context);

            Assert.Null(await repo.FindPendingDuplicateAsync("https://rejected.example.com", null, null));
            Assert.Null(await repo.FindPendingDuplicateAsync("https://draft.example.com", null, null));
        }

        [Fact]
        public async Task OtherPageOnSameSite_IsNotADuplicate()
        {
            using var context = Seeded();
            var repo = new SubmissionRepository(context);

            Assert.Null(await repo.FindPendingDuplicateAsync("https://shop.example.com/other-page", null, null));
        }

        [Fact]
        public async Task EditOfListingWithPendingEdit_IsADuplicate_EvenWithANewLink()
        {
            using var context = Seeded();
            var repo = new SubmissionRepository(context);

            var dup = await repo.FindPendingDuplicateAsync("https://brand-new-domain.example.org", 42, null);

            Assert.Equal(2, dup?.SubmissionId);
        }

        [Fact]
        public async Task ASubmissionIsNotItsOwnDuplicate()
        {
            using var context = Seeded();
            var repo = new SubmissionRepository(context);

            Assert.Null(await repo.FindPendingDuplicateAsync("https://shop.example.com", null, 1));
        }

        [Fact]
        public async Task ReviewPage_ListsEverySentSubmissionOfTheLink()
        {
            using var context = Seeded();
            context.Submissions.Add(Sub(10, "https://rejected.example.com/", SubmissionStatus.Pending));
            await context.SaveChangesAsync();
            var repo = new SubmissionRepository(context);

            var others = await repo.ListOthersWithSameLinkAsync(10, "https://rejected.example.com");

            Assert.Equal(new[] { 3 }, others.Select(o => o.SubmissionId));
        }

        private static Submission Sub(int id, string link, SubmissionStatus status, int? entryId = null) => new Submission
        {
            SubmissionId = id,
            Name = $"Sub {id}",
            Link = link,
            Description = "test",
            SubmissionStatus = status,
            DirectoryEntryId = entryId,
        };

        private static ApplicationDbContext Seeded()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"subs-{Guid.NewGuid():N}")
                .Options;
            var context = new ApplicationDbContext(options);
            context.Submissions.AddRange(
                Sub(1, "https://shop.example.com", SubmissionStatus.Pending),
                Sub(2, "https://listing.example.com", SubmissionStatus.Pending, entryId: 42),
                Sub(3, "https://rejected.example.com", SubmissionStatus.Rejected),
                Sub(4, "https://draft.example.com", SubmissionStatus.Preview));
            context.SaveChanges();
            return context;
        }
    }
}
