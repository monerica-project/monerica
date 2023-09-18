using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Tests.MockHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class CategoryRepositoryTests
    {
        private readonly Mock<DbSet<Category>> mockSet;
        private readonly Mock<IApplicationDbContext> mockContext;
        private readonly CategoryRepository repository;

        public CategoryRepositoryTests()
        {
            var mockCategories = Enumerable.Range(1, 5).Select(i => new Category
            {
                Id = i,
                Name = $"Category {i}"
            }).ToList();

            this.mockSet = new Mock<DbSet<Category>>();

            // This sets up the DbSet to work as an IQueryable with asynchronous support
            this.mockSet.As<IQueryable<Category>>().Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<Category>(mockCategories.AsQueryable().Provider));
            this.mockSet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(mockCategories.AsQueryable().Expression);
            this.mockSet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(mockCategories.AsQueryable().ElementType);
            this.mockSet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(mockCategories.GetEnumerator());

            // Here we're setting up the mock to return the expected categories as IAsyncEnumerable
            this.mockSet.As<IAsyncEnumerable<Category>>()
                .Setup(d => d.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<Category>(mockCategories.GetEnumerator()));

            this.mockContext = new Mock<IApplicationDbContext>();
            this.mockContext.Setup(m => m.Categories).Returns(this.mockSet.Object);

            this.repository = new CategoryRepository(this.mockContext.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllCategories()
        {
            // Act
            var categories = await this.repository.GetAllAsync();

            // Assert
            Assert.Equal(5, categories.Count());
        }
    }
}
