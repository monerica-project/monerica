using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Data.Tests.MockHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class DirectoryEntryRepositoryTests
    {
        private readonly DirectoryEntryRepository repository;
        private readonly Mock<IApplicationDbContext> mockContext;
        private readonly Mock<IDirectoryEntriesAuditRepository> mockAuditRepo;
        private readonly IAsyncEnumerable<DirectoryEntry> testDirectoryEntries;

        public DirectoryEntryRepositoryTests()
        {
            this.mockContext = new Mock<IApplicationDbContext>();
            this.mockAuditRepo = new Mock<IDirectoryEntriesAuditRepository>();
            this.repository = new DirectoryEntryRepository(this.mockContext.Object, this.mockAuditRepo.Object);

            var testDirectoryEntriesList = new List<DirectoryEntry>
            {
                new DirectoryEntry { Id = 1, Name = "Test1", Link = "Link1" },
                new DirectoryEntry { Id = 2, Name = "Test2", Link = "Link2" }
            };

            this.testDirectoryEntries = testDirectoryEntriesList.ToAsyncEnumerable();

            var mockSet = new Mock<DbSet<DirectoryEntry>>();
            mockSet.Setup(m => m.FindAsync(It.IsAny<object[]>()))
                   .Returns<object[]>(ids => new ValueTask<DirectoryEntry?>(testDirectoryEntriesList.FirstOrDefault(e => e.Id == (int)ids[0])));

            mockSet.As<IAsyncEnumerable<DirectoryEntry>>()
                   .Setup(m => m.GetAsyncEnumerator(CancellationToken.None))
                   .Returns(this.testDirectoryEntries.GetAsyncEnumerator(CancellationToken.None));

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.Provider)
                   .Returns(testDirectoryEntriesList.AsQueryable().Provider);

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.Expression)
                   .Returns(testDirectoryEntriesList.AsQueryable().Expression);

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.ElementType)
                   .Returns(testDirectoryEntriesList.AsQueryable().ElementType);

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.GetEnumerator())
                   .Returns(testDirectoryEntriesList.GetEnumerator());

            this.mockContext.Setup(c => c.DirectoryEntries).Returns(mockSet.Object);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectItem()
        {
            // Arrange
            _ = new Mock<IDirectoryEntriesAuditRepository>();

            // Act
            var result = await this.repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test1", result.Name);
        }
    }
}