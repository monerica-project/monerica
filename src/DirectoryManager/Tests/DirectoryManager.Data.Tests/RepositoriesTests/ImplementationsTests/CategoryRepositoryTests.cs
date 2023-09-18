using Moq;
using Microsoft.EntityFrameworkCore;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class CategoryRepositoryTests
    {
        private readonly Mock<DbSet<Category>> _mockSet;
        private readonly Mock<ApplicationDbContext> _mockContext;
        private readonly CategoryRepository _repository;

        public CategoryRepositoryTests()
        {
            _mockSet = new Mock<DbSet<Category>>();
            _mockContext = new Mock<ApplicationDbContext>();

            _mockContext.Setup(m => m.Categories).Returns(_mockSet.Object);
            _repository = new CategoryRepository(_mockContext.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllCategories()
        {
            // Arrange
            var mockCategories = Enumerable.Range(1, 5).Select(i => new Category
            {
                Id = i,
                Name = $"Category {i}"
            }).AsQueryable();

            _mockSet.As<IQueryable<Category>>().Setup(m => m.Provider).Returns(mockCategories.Provider);
            _mockSet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(mockCategories.Expression);
            _mockSet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(mockCategories.ElementType);
            _mockSet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(mockCategories.GetEnumerator());

            // Act
            var categories = await _repository.GetAllAsync();

            // Assert
            Assert.Equal(5, categories.Count());
        }
    }
}
