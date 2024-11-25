using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class SubCategoryRepositoryTests
    {
        private readonly Mock<IApplicationDbContext> mockContext;
        private readonly SubcategoryRepository repository;

        public SubCategoryRepositoryTests()
        {
            this.mockContext = new Mock<IApplicationDbContext>();

            var data = new List<Subcategory>
            {
                new Subcategory { SubCategoryId = 1, Name = "SubCategory1" },
                new Subcategory { SubCategoryId = 2, Name = "SubCategory2" }
            };

            var mockDbSet = MockHelpers.MockHelpers.GetQueryableMockDbSet(data);
            this.mockContext.Setup(c => c.SubCategories).Returns(mockDbSet.Object);

            this.repository = new SubcategoryRepository(this.mockContext.Object);
        }

        [Fact(Skip = "Skipping this test due to ExecuteAsync issue.")]
        public async Task GetByIdAsync_InvalidId_ReturnsNull()
        {
            // Act
            var result = await this.repository.GetByIdAsync(999); // Nonexistent Id

            // Assert
            Assert.Null(result);
        }
    }
}
