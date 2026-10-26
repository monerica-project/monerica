using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class SubmissionBlockedDomainRepositoryTests
    {
        [Fact]
        public async Task Create_StoresNormalizedDomain_AndRejectsDuplicates()
        {
            using var context = NewContext();
            var repo = new SubmissionBlockedDomainRepository(context);

            var first = await repo.CreateAsync(new SubmissionBlockedDomain { Domain = "https://www.Muse.ai/files/x" });
            var again = await repo.CreateAsync(new SubmissionBlockedDomain { Domain = "muse.ai" });

            Assert.Equal("muse.ai", first?.Domain);
            Assert.Null(again);
        }

        [Fact]
        public async Task Create_RejectsInvalid_AndDisabledAreNotEnforced()
        {
            using var context = NewContext();
            var repo = new SubmissionBlockedDomainRepository(context);

            Assert.Null(await repo.CreateAsync(new SubmissionBlockedDomain { Domain = "not a domain" }));

            var d = await repo.CreateAsync(new SubmissionBlockedDomain { Domain = "muse.ai" });
            await repo.ToggleEnabledAsync(d!.SubmissionBlockedDomainId);

            Assert.Empty(await repo.GetEnabledAsync());
        }

        private static ApplicationDbContext NewContext() =>
            new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"blocked-domains-{Guid.NewGuid():N}").Options);
    }
}
