using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class DirectoryEntryRepositoryTests
    {
        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectItem()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                // Seed the in-memory database
                var category = new Category { CategoryId = 1, Name = "TestCategory", CategoryKey = "test-category" };
                var subCategory = new Subcategory { SubCategoryId = 1, Name = "TestSubCategory", SubCategoryKey = "test-subcategory", Category = category };
                var directoryEntry = new DirectoryEntry { DirectoryEntryId = 1, Name = "TestEntry", DirectoryEntryKey = "test-entry", SubCategory = subCategory };

                context.Categories.Add(category);
                context.Subcategories.Add(subCategory);
                context.DirectoryEntries.Add(directoryEntry);
                await context.SaveChangesAsync();

                // Create a mock for the IDirectoryEntriesAuditRepository
                var mockAuditRepository = new Mock<IDirectoryEntriesAuditRepository>();

                var repository = new DirectoryEntryRepository(context, mockAuditRepository.Object);

                // Act
                var result = await repository.GetByIdAsync(1);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("TestEntry", result.Name);
                Assert.Equal("test-entry", result.DirectoryEntryKey);
                Assert.Equal("TestSubCategory", result?.SubCategory?.Name);
                Assert.Equal("TestCategory", result?.SubCategory?.Category.Name);
            }
        }
    }
}