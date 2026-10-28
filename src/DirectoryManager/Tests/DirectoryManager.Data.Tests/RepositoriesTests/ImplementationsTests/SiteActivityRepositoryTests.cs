using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.ProblemReports;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.VerificationRequests;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    // Admin activity report source: only things PEOPLE submitted, in the date range.
    public class SiteActivityRepositoryTests
    {
        private static readonly DateTime From = new (2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new (2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime In = new (2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Counts_every_kind_people_submit_and_splits_new_listings_from_edits()
        {
            using var context = Seeded();
            var items = await new SiteActivityRepository(context).GetActivityAsync(From, To);

            int Count(SiteActivityType t) => items.Count(i => i.Type == t);
            Assert.Equal(2, Count(SiteActivityType.NewListing));    // pending new + an approved new one now linked to its listing
            Assert.Equal(2, Count(SiteActivityType.ListingEdit));   // a person's edit + a rejected one
            Assert.Equal(1, Count(SiteActivityType.Review));
            Assert.Equal(1, Count(SiteActivityType.ReviewReply));
            Assert.Equal(1, Count(SiteActivityType.ProblemReport));
            Assert.Equal(1, Count(SiteActivityType.VerificationRequest));
            Assert.Equal(8, items.Count);
        }

        [Fact]
        public async Task Range_end_is_exclusive()
        {
            using var context = Seeded();
            var items = await new SiteActivityRepository(context).GetActivityAsync(From, In);

            Assert.Empty(items);
        }

        [Theory]
        [InlineData(null, null, false)]   // not linked: new listing
        [InlineData(5, -10, true)]        // listing existed before: edit
        [InlineData(5, 3, false)]         // listing created after (approved new-listing submission)
        [InlineData(5, 0, false)]         // created at the same moment: new
        [InlineData(5, null, true)]       // listing since deleted: still an edit
        public void New_vs_edit_is_decided_by_when_the_listing_was_created(int? entryId, int? entryCreatedOffsetDays, bool expectedEdit)
        {
            DateTime? entryCreated = entryCreatedOffsetDays.HasValue ? In.AddDays(entryCreatedOffsetDays.Value) : null;
            Assert.Equal(expectedEdit, SiteActivityRepository.IsEdit(entryId, entryCreated, In));
        }

        private static ApplicationDbContext Seeded()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"activity-{Guid.NewGuid():N}")
                .Options;

            using (var seed = new ApplicationDbContext(options))
            {
                // Listings 7 and 8 existed before October; listing 9 was created by approving submission 8.
                seed.DirectoryEntries.AddRange(
                    new DirectoryEntry { DirectoryEntryId = 7, DirectoryEntryKey = "seven", Name = "Seven", Link = "https://seven.example", CreateDate = From.AddDays(-100) },
                    new DirectoryEntry { DirectoryEntryId = 8, DirectoryEntryKey = "eight", Name = "Eight", Link = "https://eight.example", CreateDate = From.AddDays(-50) },
                    new DirectoryEntry { DirectoryEntryId = 9, DirectoryEntryKey = "nine", Name = "Nine", Link = "https://nine.example", CreateDate = In.AddDays(2) });

                seed.Submissions.AddRange(
                    new Submission { SubmissionId = 1, CreateDate = In, SubmissionStatus = SubmissionStatus.Pending, DirectoryEntryId = null },
                    new Submission { SubmissionId = 2, CreateDate = In, SubmissionStatus = SubmissionStatus.Approved, DirectoryEntryId = 7 },
                    new Submission { SubmissionId = 3, CreateDate = In, SubmissionStatus = SubmissionStatus.Rejected, DirectoryEntryId = 8 },
                    new Submission { SubmissionId = 8, CreateDate = In, SubmissionStatus = SubmissionStatus.Approved, DirectoryEntryId = 9 },

                    // Not counted: a draft, the two automated checkers, and one outside the range.
                    new Submission { SubmissionId = 4, CreateDate = In, SubmissionStatus = SubmissionStatus.Preview, DirectoryEntryId = null },
                    new Submission { SubmissionId = 5, CreateDate = In, SubmissionStatus = SubmissionStatus.Pending, DirectoryEntryId = 7, NoteToAdmin = "(automated submission)" },
                    new Submission { SubmissionId = 6, CreateDate = In, SubmissionStatus = SubmissionStatus.Pending, DirectoryEntryId = 7, NoteToAdmin = "[auto-detail-check] country" },
                    new Submission { SubmissionId = 7, CreateDate = To, SubmissionStatus = SubmissionStatus.Pending, DirectoryEntryId = null });

                seed.DirectoryEntryReviews.Add(new DirectoryEntryReview { DirectoryEntryReviewId = 1, DirectoryEntryId = 7, CreateDate = In });
                seed.DirectoryEntryReviewComments.Add(new DirectoryEntryReviewComment { DirectoryEntryReviewCommentId = 1, DirectoryEntryReviewId = 1, CreateDate = In });
                seed.ProblemReports.Add(new ProblemReport { ProblemReportId = 1, DirectoryEntryId = 7, CreateDate = In });
                seed.VerificationRequests.Add(new VerificationRequest { VerificationRequestId = 1, DirectoryEntryId = 7, CreateDate = In });
                seed.SaveChanges();
            }

            return new ApplicationDbContext(options);
        }
    }
}
