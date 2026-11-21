using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class BrowseListsHideScamsTests
    {
        [Fact]
        public async Task CategoryAndSubcategoryLists_LeaveOutScams_ButKeepQuestionable()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"browse-scams-{Guid.NewGuid():N}")
                .Options;

            using var context = new ApplicationDbContext(options);
            var category = new Category { CategoryId = 1, Name = "Businesses", CategoryKey = "businesses" };
            var sub = new Subcategory { SubCategoryId = 1, Name = "Legal", SubCategoryKey = "legal", Category = category };
            context.Categories.Add(category);
            context.Subcategories.Add(sub);
            context.DirectoryEntries.AddRange(
                new DirectoryEntry { DirectoryEntryId = 1, Name = "Good", DirectoryEntryKey = "good", SubCategory = sub, DirectoryStatus = DirectoryStatus.Verified },
                new DirectoryEntry { DirectoryEntryId = 2, Name = "Doubtful", DirectoryEntryKey = "doubtful", SubCategory = sub, DirectoryStatus = DirectoryStatus.Questionable },
                new DirectoryEntry { DirectoryEntryId = 3, Name = "Thief", DirectoryEntryKey = "thief", SubCategory = sub, DirectoryStatus = DirectoryStatus.Scam });
            await context.SaveChangesAsync();

            var repository = new DirectoryEntryRepository(context, new Mock<IDirectoryEntriesAuditRepository>().Object);

            var byCategory = await repository.ListEntriesByCategoryAsync(1, 1, 50);
            var bySubcategory = await repository.GetActiveEntriesBySubcategoryPagedAsync(1, 1, 50);

            Assert.Equal(2, byCategory.TotalCount);
            Assert.Equal(2, bySubcategory.TotalCount);
            Assert.DoesNotContain(byCategory.Items, e => e.Name == "Thief");
            Assert.DoesNotContain(bySubcategory.Items, e => e.Name == "Thief");
            Assert.Contains(bySubcategory.Items, e => e.Name == "Doubtful");
        }
    }
}
