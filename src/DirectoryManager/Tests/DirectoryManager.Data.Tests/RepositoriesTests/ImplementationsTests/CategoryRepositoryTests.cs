using Moq;
using Microsoft.EntityFrameworkCore;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using Xunit;
using System.Linq;
using System.Threading.Tasks;
using DirectoryManager.Data.Tests.MockHelpers;
using System.Collections.Generic;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class CategoryRepositoryTests
    {
        private readonly Mock<DbSet<Category>> _mockSet;
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly CategoryRepository _repository;

        public CategoryRepositoryTests()
        {
            var mockCategories = Enumerable.Range(1, 5).Select(i => new Category
            {
                Id = i,
                Name = $"Category {i}"
            }).ToList();

            _mockSet = new Mock<DbSet<Category>>();

            // This sets up the DbSet to work as an IQueryable with asynchronous support
            _mockSet.As<IQueryable<Category>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<Category>(mockCategories.AsQueryable().Provider));
            _mockSet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(mockCategories.AsQueryable().Expression);
            _mockSet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(mockCategories.AsQueryable().ElementType);
            _mockSet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(mockCategories.GetEnumerator());

            // Here we're setting up the mock to return the expected categories as IAsyncEnumerable
            _mockSet.As<IAsyncEnumerable<Category>>()
                .Setup(d => d.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<Category>(mockCategories.GetEnumerator()));

            _mockContext = new Mock<IApplicationDbContext>();
            _mockContext.Setup(m => m.Categories).Returns(_mockSet.Object);

            _repository = new CategoryRepository(_mockContext.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllCategories()
        {
            // Act
            var categories = await _repository.GetAllAsync();

            // Assert
            Assert.Equal(5, categories.Count());
        }
    }
}
