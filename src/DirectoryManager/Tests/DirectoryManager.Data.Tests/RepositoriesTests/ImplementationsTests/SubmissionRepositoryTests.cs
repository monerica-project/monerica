using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Tests.MockHelpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class SubmissionRepositoryTests
    {
        private readonly Mock<IApplicationDbContext> mockContext;
        private readonly Mock<DbSet<Submission>> mockDbSet;
        private readonly SubmissionRepository repository;
        private readonly Mock<IDbSetAsyncWrapper<Submission>> mockDbSetAsyncWrapper;

        public SubmissionRepositoryTests()
        {
            var testData = new List<Submission>
            {
                new Submission { Id = 1, /*... other properties ...*/ },
                new Submission { Id = 2, /*... other properties ...*/ }

                // ... You can add more test data if required ...
            };

            this.mockDbSet = DirectoryManager.Data.Tests.MockHelpers.MockHelpers.GetQueryableMockDbSet(testData);
            this.mockContext = new Mock<IApplicationDbContext>();
            this.mockContext.Setup(x => x.Submissions).Returns(this.mockDbSet.Object);

            // Mocking the FindAsync method
            this.mockDbSet.Setup(m => m.FindAsync(It.IsAny<object[]>()))
                      .Returns<object[]>(ids => new ValueTask<Submission?>(testData.FirstOrDefault(e => e.Id == (int)ids[0])));

            this.mockDbSetAsyncWrapper = new Mock<IDbSetAsyncWrapper<Submission>>();
            this.mockDbSetAsyncWrapper.Setup(d => d.ToListAsync(It.IsAny<CancellationToken>()))
                                  .ReturnsAsync(testData);

            // Ensure that in the actual SubmissionRepository, you replace calls to the DbSet's ToListAsync
            // with calls to the IDbSetAsyncWrapper's ToListAsync.
            // For this example, you will need to modify the repository to take the wrapper as an argument
            // or find another way to use the wrapper's methods instead of the DbSet's methods.
            this.repository = new SubmissionRepository(this.mockContext.Object);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSubmission()
        {
            var result = await this.repository.GetByIdAsync(1);
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task AddAsync_ShouldAddSubmission()
        {
            var newSubmission = new Submission { Id = 3, /*... other properties ...*/ };
            await this.repository.AddAsync(newSubmission);

            this.mockDbSet.Verify(x => x.AddAsync(It.IsAny<Submission>(), default), Times.Once);
            this.mockContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateSubmission()
        {
            var submissionToUpdate = new Submission { Id = 1, /*... other properties ...*/ };
            await this.repository.UpdateAsync(submissionToUpdate);

            this.mockContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteSubmission()
        {
            await this.repository.DeleteAsync(1);

            this.mockDbSet.Verify(x => x.Remove(It.IsAny<Submission>()), Times.Once);
            this.mockContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }
    }
}
