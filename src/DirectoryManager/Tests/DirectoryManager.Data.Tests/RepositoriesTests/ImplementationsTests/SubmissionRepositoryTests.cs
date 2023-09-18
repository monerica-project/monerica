using Xunit;
using Moq;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DirectoryManager.Data.Tests.MockHelpers;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
 
    public class SubmissionRepositoryTests
    {
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly Mock<DbSet<Submission>> _mockDbSet;
        private readonly SubmissionRepository _repository;
        private readonly Mock<IDbSetAsyncWrapper<Submission>> _mockDbSetAsyncWrapper;

        public SubmissionRepositoryTests()
        {
            var testData = new List<Submission>
            {
                new Submission { Id = 1, /*... other properties ...*/ },
                new Submission { Id = 2, /*... other properties ...*/ }
                // ... You can add more test data if required ...
            };

            _mockDbSet = DirectoryManager.Data.Tests.MockHelpers.MockHelpers.GetQueryableMockDbSet(testData);
            _mockContext = new Mock<IApplicationDbContext>();
            _mockContext.Setup(x => x.Submissions).Returns(_mockDbSet.Object);

            // Mocking the FindAsync method
            _mockDbSet.Setup(m => m.FindAsync(It.IsAny<object[]>()))
                      .Returns<object[]>(ids => new ValueTask<Submission?>(testData.FirstOrDefault(e => e.Id == (int)ids[0])));

            _mockDbSetAsyncWrapper = new Mock<IDbSetAsyncWrapper<Submission>>();
            _mockDbSetAsyncWrapper.Setup(d => d.ToListAsync(It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(testData);

            // Ensure that in the actual SubmissionRepository, you replace calls to the DbSet's ToListAsync 
            // with calls to the IDbSetAsyncWrapper's ToListAsync.
            // For this example, you will need to modify the repository to take the wrapper as an argument 
            // or find another way to use the wrapper's methods instead of the DbSet's methods.

            _repository = new SubmissionRepository(_mockContext.Object);
        }


        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSubmission()
        {
            var result = await _repository.GetByIdAsync(1);
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task AddAsync_ShouldAddSubmission()
        {
            var newSubmission = new Submission { Id = 3, /*... other properties ...*/ };
            await _repository.AddAsync(newSubmission);

            _mockDbSet.Verify(x => x.AddAsync(It.IsAny<Submission>(), default), Times.Once);
            _mockContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateSubmission()
        {
            var submissionToUpdate = new Submission { Id = 1, /*... other properties ...*/ };
            await _repository.UpdateAsync(submissionToUpdate);

            _mockContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteSubmission()
        {
            await _repository.DeleteAsync(1);

            _mockDbSet.Verify(x => x.Remove(It.IsAny<Submission>()), Times.Once);
            _mockContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        // You can add more tests for scenarios not covered above.
    }
}
