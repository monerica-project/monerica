using System.Reflection;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Tests.Seo
{
    // The "Open Source" tag was retired 2026-11-13: being open source is now the source code link.
    // Its old public page must 301 to the filter view of listings that have one.
    public class RetiredOpenSourceTagTests
    {
        [Theory] // HAPPY: old tag page (and its pages) 301 to the source code filter
        [InlineData(1)]
        [InlineData(3)]
        public async Task OldOpenSourceTagPage_RedirectsPermanentlyToSourceCodeFilter(int page)
        {
            using var db = NewDb();
            var controller = NewTagController(db);

            var result = await controller.Index("open-source", page);

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.True(redirect.Permanent);
            Assert.Equal("/filter?HasSourceCode=true", redirect.Url);
        }

        [Fact] // SAD: an unknown tag is still a 404, not a redirect
        public async Task UnknownTag_IsNotFound()
        {
            using var db = NewDb();
            var controller = NewTagController(db);

            Assert.IsType<NotFoundResult>(await controller.Index("no-such-tag"));
        }

        [Fact] // HAPPY: the filter facet keeps only listings with a source code link
        public async Task HasSourceCodeFilter_ReturnsOnlyListingsWithASourceCodeLink()
        {
            using var db = NewDb();
            var category = new Category { CategoryId = 1, Name = "Software", CategoryKey = "software", IsEnabled = true };
            db.Categories.Add(category);
            db.Subcategories.Add(new Subcategory { SubCategoryId = 1, Name = "Tools", SubCategoryKey = "tools", Category = category, CategoryId = 1, IsEnabled = true });
            db.DirectoryEntries.AddRange(
                Entry(1, "Repo Tool", "https://github.com/acme/tool"),
                Entry(2, "Closed Shop", null),
                Entry(3, "Blank Link", "   "));
            await db.SaveChangesAsync();

            var repo = new DirectoryEntryRepository(db, new DirectoryEntriesAuditRepository(db));
            var result = await repo.FilterAsync(new DirectoryFilterQuery { HasSourceCode = true, Page = 1, PageSize = 24 });

            Assert.Equal(1, result.TotalCount);
            Assert.Equal("Repo Tool", Assert.Single(result.Items).Name);
        }

        private static ApplicationDbContext NewDb() => new (new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"retired-tag-{Guid.NewGuid()}")
            .Options);

        private static DirectoryEntry Entry(int id, string name, string? source) => new ()
        {
            DirectoryEntryId = id,
            Name = name,
            DirectoryEntryKey = name.ToLowerInvariant().Replace(' ', '-'),
            Link = $"https://example{id}.com",
            SourceCodeLink = source,
            DirectoryStatus = DirectoryStatus.Admitted,
            SubCategoryId = 1,
        };

        private static TagController NewTagController(ApplicationDbContext db) => new (
            new TagRepository(db),
            new DirectoryEntryTagRepository(db),
            NullProxy<ICacheService>.Create(),
            new SponsoredListingRepository(db),
            new DirectoryEntryReviewRepository(db),
            null!, // selections: unused by the controller
            new MemoryCache(new MemoryCacheOptions()));

        private class NullProxy<T> : DispatchProxy
            where T : class
        {
            public static T Create() => Create<T, NullProxy<T>>();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
