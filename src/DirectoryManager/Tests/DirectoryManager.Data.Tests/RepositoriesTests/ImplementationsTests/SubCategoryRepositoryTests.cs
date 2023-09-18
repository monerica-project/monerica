using DirectoryManager.Data.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class SubCategoryRepositoryTests
    {
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly DbSet<SubCategory> _mockDbSet;
        private readonly SubCategoryRepository _repository;

        public SubCategoryRepositoryTests()
        {
            _mockContext = new Mock<IApplicationDbContext>();

            // Sample data
            var data = new List<SubCategory>
            {
                new SubCategory { Id = 1, Name = "SubCategory1" },
                new SubCategory { Id = 2, Name = "SubCategory2" }
            }.AsQueryable();

            _mockDbSet = MockHelpers.MockHelpers.GetQueryableMockDbSet(data).Object;
            _mockContext.Setup(c => c.SubCategories).Returns(_mockDbSet);

            _repository = new SubCategoryRepository(_mockContext.Object);
        }

        [Fact]
        public async Task GetByIdAsync_InvalidId_ReturnsNull()
        {
            var result = await _repository.GetByIdAsync(999); // Nonexistent Id
            Assert.Null(result);
        }
    }
}
