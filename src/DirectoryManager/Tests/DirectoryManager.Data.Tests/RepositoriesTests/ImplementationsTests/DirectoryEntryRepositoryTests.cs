using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Tests.MockHelpers;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class DirectoryEntryRepositoryTests
    {
        private readonly DirectoryEntryRepository _repository;
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly Mock<IDirectoryEntriesAuditRepository> _mockAuditRepo;
        private readonly IAsyncEnumerable<DirectoryEntry> _testDirectoryEntries;

        public DirectoryEntryRepositoryTests()
        {
            _mockContext = new Mock<IApplicationDbContext>();
            _mockAuditRepo = new Mock<IDirectoryEntriesAuditRepository>();
            _repository = new DirectoryEntryRepository(_mockContext.Object, _mockAuditRepo.Object);

            var _testDirectoryEntriesList = new List<DirectoryEntry>
            {
                new DirectoryEntry { Id = 1, Name = "Test1", Link = "Link1" },
                new DirectoryEntry { Id = 2, Name = "Test2", Link = "Link2" }
            };
      
            _testDirectoryEntries = _testDirectoryEntriesList.ToAsyncEnumerable();

            var mockSet = new Mock<DbSet<DirectoryEntry>>();
            mockSet.Setup(m => m.FindAsync(It.IsAny<object[]>()))
                   .Returns<object[]>(ids => new ValueTask<DirectoryEntry?>(_testDirectoryEntriesList.FirstOrDefault(e => e.Id == (int)ids[0])));

            mockSet.As<IAsyncEnumerable<DirectoryEntry>>()
                   .Setup(m => m.GetAsyncEnumerator(new CancellationToken()))
                   .Returns(_testDirectoryEntries.GetAsyncEnumerator(new CancellationToken()));

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.Provider)
                   .Returns(_testDirectoryEntriesList.AsQueryable().Provider);

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.Expression)
                   .Returns(_testDirectoryEntriesList.AsQueryable().Expression);

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.ElementType)
                   .Returns(_testDirectoryEntriesList.AsQueryable().ElementType);

            mockSet.As<IQueryable<DirectoryEntry>>()
                   .Setup(m => m.GetEnumerator())
                   .Returns(_testDirectoryEntriesList.GetEnumerator());

            _mockContext.Setup(c => c.DirectoryEntries).Returns(mockSet.Object);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectItem()
        {
            // Arrange
            var mockAuditRepo = new Mock<IDirectoryEntriesAuditRepository>();
            var repository = new DirectoryEntryRepository(_mockContext.Object, mockAuditRepo.Object);

            // Act
            var result = await repository.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test1", result.Name);
        }
    }
}