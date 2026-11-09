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
    
        [Theory]
        [InlineData("https://www.Example.com/", "example.com")]
        [InlineData("  GhostSwap ", "ghostswap")]
        [InlineData("http://xmr.cards/order/", "xmr.cards/order")]
        public void NormalizeAdminQuery_StripsSchemeWwwAndSlash(string input, string expected) =>
            Assert.Equal(expected, DirectoryEntryRepository.NormalizeAdminQuery(input));

        [Fact]
        public async Task AdminSearchAsync_FindsByPartialNameOrUrl_InAnyStatus()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"admin-search-{Guid.NewGuid():N}")
                .Options;
            using var context = new ApplicationDbContext(options);
            context.Categories.Add(new Category { CategoryId = 1, Name = "Exchanges", CategoryKey = "exchanges" });
            context.Subcategories.Add(new Subcategory { SubCategoryId = 1, CategoryId = 1, Name = "Instant Swaps", SubCategoryKey = "instant-swaps" });
            context.DirectoryEntries.AddRange(
                new DirectoryEntry { DirectoryEntryId = 10, Name = "GhostSwap", DirectoryEntryKey = "ghostswap", Link = "https://ghostswap.io/", SubCategoryId = 1, DirectoryStatus = DirectoryManager.Data.Enums.DirectoryStatus.Verified },
                new DirectoryEntry { DirectoryEntryId = 11, Name = "Old Swap", DirectoryEntryKey = "old-swap", Link = "https://oldswap.example/", Link2 = "http://ghostmirror.onion", SubCategoryId = 1, DirectoryStatus = DirectoryManager.Data.Enums.DirectoryStatus.Removed },
                new DirectoryEntry { DirectoryEntryId = 12, Name = "Scammy", DirectoryEntryKey = "scammy", Link = "https://scammy.example/", SubCategoryId = 1, DirectoryStatus = DirectoryManager.Data.Enums.DirectoryStatus.Scam });
            await context.SaveChangesAsync();
            var repository = new DirectoryEntryRepository(context, new Mock<IDirectoryEntriesAuditRepository>().Object);

            var byName = await repository.AdminSearchAsync("ghost");
            Assert.Equal(new[] { 10, 11 }, byName.Select(e => e.DirectoryEntryId).ToArray());

            var byUrl = await repository.AdminSearchAsync("https://www.oldswap.example/");
            Assert.Equal(11, Assert.Single(byUrl).DirectoryEntryId);

            var scam = await repository.AdminSearchAsync("SCAMMY.EXAMPLE");
            Assert.Equal(12, Assert.Single(scam).DirectoryEntryId);

            var byId = await repository.AdminSearchAsync("12");
            Assert.Equal(12, byId.First().DirectoryEntryId);

            Assert.Empty(await repository.AdminSearchAsync("   "));
        }
}
}