using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Tests.RepositoriesTests.ImplementationsTests
{
    public class AiCallLogRepositoryTests
    {
        [Fact]
        public async Task Page_IsNewestFirst_AndFiltersBySubmissionAndErrors()
        {
            using var context = NewContext();
            var now = DateTime.UtcNow;
            context.AiCallLogs.AddRange(
                Log(1, now.AddMinutes(-30), submissionId: 10),
                Log(2, now.AddMinutes(-20), submissionId: 11, error: "API HTTP 529"),
                Log(3, now.AddMinutes(-10), submissionId: 10));
            await context.SaveChangesAsync();
            var repo = new AiCallLogRepository(context);

            var (all, total) = await repo.GetPageAsync(1, 50, null, false);
            Assert.Equal(3, total);
            Assert.Equal(new[] { 3, 2, 1 }, all.Select(x => x.Id));
            Assert.Null(all[0].RequestText); // list rows skip the large texts

            var (forTen, tenTotal) = await repo.GetPageAsync(1, 50, 10, false);
            Assert.Equal(2, tenTotal);
            Assert.All(forTen, x => Assert.Equal(10, x.SubmissionId));

            var (errors, errTotal) = await repo.GetPageAsync(1, 50, null, true);
            Assert.Equal(1, errTotal);
            Assert.Equal(2, errors[0].Id);

            var full = await repo.GetByIdAsync(3);
            Assert.Equal("dossier 3", full!.RequestText);
        }

        [Fact]
        public async Task DailyTotals_CountCallsErrorsAndTokens()
        {
            using var context = NewContext();
            var today = DateTime.UtcNow.Date.AddHours(1);
            context.AiCallLogs.AddRange(
                Log(1, today, 1, inTok: 100, outTok: 10),
                Log(2, today, 2, inTok: 200, outTok: 20, error: "x"),
                Log(3, today.AddDays(-1), 3, inTok: 50, outTok: 5),
                Log(4, today.AddDays(-90), 4));
            await context.SaveChangesAsync();

            var days = await new AiCallLogRepository(context).GetDailyTotalsAsync(30);

            Assert.Equal(2, days.Count);
            Assert.Equal(new AiCallDailyTotal(today.Date, 2, 1, 300, 30), days[0]);
            Assert.Equal(1, days[1].Calls);
        }

        private static AiCallLog Log(int id, DateTime when, int submissionId, int inTok = 1, int outTok = 1, string? error = null) => new ()
        {
            Id = id,
            CreatedUtc = when,
            Feature = AiCallLog.FeatureSubmissionReview,
            SubmissionId = submissionId,
            Model = "test-model",
            InputTokens = inTok,
            OutputTokens = outTok,
            Error = error,
            RequestText = $"dossier {id}",
            ResponseText = "{}",
        };

        private static ApplicationDbContext NewContext() =>
            new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ai-log-{Guid.NewGuid():N}").Options);
    }
}
