using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    // Uses EF's in-memory provider: the repository's async queries (FirstOrDefaultAsync + Include)
    // can't run against a mocked IQueryable, which is why this test used to be skipped.
    public class SubCategoryRepositoryTests
    {
        [Fact]
        public async Task GetByIdAsync_InvalidId_ReturnsNull()
        {
            using var context = Seeded();
            var repository = new SubcategoryRepository(context);

            var result = await repository.GetByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByIdAsync_ValidId_ReturnsSubcategoryWithCategory()
        {
            using var context = Seeded();
            var repository = new SubcategoryRepository(context);

            var result = await repository.GetByIdAsync(2);

            Assert.NotNull(result);
            Assert.Equal("SubCategory2", result!.Name);
            Assert.NotNull(result.Category);
            Assert.Equal("Category1", result.Category!.Name);
        }

        private static ApplicationDbContext Seeded()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"subcat-{Guid.NewGuid():N}")
                .Options;
            using (var seed = new ApplicationDbContext(options))
            {
                seed.Categories.Add(new Category { CategoryId = 1, Name = "Category1", CategoryKey = "category1" });
                seed.Subcategories.AddRange(
                    new Subcategory { SubCategoryId = 1, Name = "SubCategory1", SubCategoryKey = "sub1", CategoryId = 1 },
                    new Subcategory { SubCategoryId = 2, Name = "SubCategory2", SubCategoryKey = "sub2", CategoryId = 1 });
                seed.SaveChanges();
            }

            // A fresh context, so Include must actually load the category (not reuse tracked entities).
            return new ApplicationDbContext(options);
        }
    }
}
