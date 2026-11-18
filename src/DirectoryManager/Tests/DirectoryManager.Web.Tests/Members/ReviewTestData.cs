using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Tests.Members
{
    /// <summary>Listings, reviews and a member review service over a test database.</summary>
    public static class ReviewTestData
    {
        public const string GoodBody =
            "Swapped a small amount and it arrived quickly. Support answered within the hour and fees were as listed on the site.";

        private static int nextId = 1000;

        public static async Task<DirectoryEntry> AddEntryAsync(
            ApplicationDbContext db,
            bool requireVerification = false,
            bool requireDetails = false,
            bool reviewsDisabled = false,
            DirectoryStatus status = DirectoryStatus.Admitted)
        {
            var id = Interlocked.Increment(ref nextId);
            db.Categories.Add(new Category { CategoryId = id, Name = "Exchanges " + id, CategoryKey = "exchanges-" + id, IsEnabled = true });
            db.Subcategories.Add(new Subcategory
            {
                SubCategoryId = id,
                CategoryId = id,
                Name = "Swaps " + id,
                SubCategoryKey = "swaps-" + id,
                IsEnabled = true,
                RequireReviewVerification = requireVerification,
            });
            var entry = new DirectoryEntry
            {
                DirectoryEntryId = id,
                Name = "Alpha Swap " + id,
                DirectoryEntryKey = "alpha-swap-" + id,
                Link = "https://alpha" + id + ".example",
                SubCategoryId = id,
                DirectoryStatus = status,
                ReviewsDisabled = reviewsDisabled,
                RequireReviewVerificationDetails = requireDetails,
                CreateDate = DateTime.UtcNow.AddDays(-100),
            };
            db.DirectoryEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        }

        public static DirectoryEntryReview MemberReview(ApplicationUser member, DirectoryEntry entry, ReviewModerationStatus status) => new ()
        {
            DirectoryEntryId = entry.DirectoryEntryId,
            AuthorUserId = member.Id,
            AuthorUsername = member.UserName,
            AuthorFingerprint = MemberAuthor.AuthorKey(member.Id),
            Rating = 4,
            Body = GoodBody + " " + Guid.NewGuid().ToString("N"),
            ModerationStatus = status,
            CreateDate = DateTime.UtcNow.AddDays(-3),
        };

        public static DirectoryEntryReview PgpReview(DirectoryEntry entry, ReviewModerationStatus status) => new ()
        {
            DirectoryEntryId = entry.DirectoryEntryId,
            AuthorFingerprint = "D8054E3A0F1B2C3D4E5F60718293A4B5C6D7E8F9",
            Rating = 5,
            Body = "A PGP-verified review with enough words to pass the length rule. " + Guid.NewGuid().ToString("N"),
            ModerationStatus = status,
            CreateDate = DateTime.UtcNow.AddDays(-5),
        };

        public static MemberReviewService Service(ApplicationDbContext db, TimeProvider? clock = null) => new (
            db,
            new UserContentModerationService(new NoBlacklist()),
            new DirectoryEntryReviewRepository(db),
            new DirectoryEntryReviewCommentRepository(db),
            clock);

        private sealed class NoBlacklist : ISearchBlacklistCache
        {
            public Task<HashSet<string>> GetTermsAsync(CancellationToken ct = default) => Task.FromResult(new HashSet<string>());

            public void Invalidate()
            {
            }
        }
    }
}
