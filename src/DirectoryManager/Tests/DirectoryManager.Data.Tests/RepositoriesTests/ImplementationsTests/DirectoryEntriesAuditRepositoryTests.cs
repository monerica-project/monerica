using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class DirectoryEntriesAuditRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_CreatesNewDirectoryEntriesAudit()
        {
            // Arrange
            var auditToCreate = new DirectoryEntriesAudit { DirectoryEntryAuditId = 1, Name = "NewAudit" };

            var mockContext = new Mock<IApplicationDbContext>();
            var mockDbSet = new Mock<DbSet<DirectoryEntriesAudit>>();

            mockContext.Setup(c => c.DirectoryEntriesAudit).Returns(mockDbSet.Object);

            var repository = new DirectoryEntriesAuditRepository(mockContext.Object);

            // Act
            await repository.CreateAsync(auditToCreate);

            // Assert
            mockDbSet.Verify(db => db.AddAsync(auditToCreate, default), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }
    }
}
