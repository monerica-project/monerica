using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubmissionRepository : ISubmissionRepository
    {
        private readonly IApplicationDbContext context;

        public SubmissionRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<Submission>> GetAllAsync()
        {
            return await this.context.Submissions
                                 .OrderByDescending(x => x.CreateDate)
                                 .ToListAsync();
        }

        public async Task<Submission?> GetByIdAsync(int submissionId)
        {
            return await this.context.Submissions.FindAsync(submissionId);
        }

        public async Task<Submission?> GetByLinkAndStatusAsync(string link1, SubmissionStatus submissionStatus = SubmissionStatus.Pending)
        {
            if (string.IsNullOrWhiteSpace(link1))
            {
                return default;
            }

            return await this.context
                             .Submissions
                             .Where(x => x.Link == link1 && x.SubmissionStatus == submissionStatus)
                             .FirstOrDefaultAsync();
        }

        public async Task<Submission> CreateAsync(Submission submission)
        {
            // Ensure only the ID is set for the foreign key, and EF does not track the SubCategory entity itself
            submission.SubCategory = null;
            submission.CreateDate = DateTime.UtcNow;
            await this.context.Submissions.AddAsync(submission);
            await this.context.SaveChangesAsync();

            return submission;
        }

        public async Task UpdateAsync(Submission submission)
        {
            this.context.Submissions.Update(submission);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int submissionId)
        {
            var submission = await this.GetByIdAsync(submissionId);
            if (submission != null)
            {
                this.context.Submissions.Remove(submission);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<int> GetByStatus(SubmissionStatus status)
        {
            return await this.context.Submissions
                                 .Where(x => x.SubmissionStatus == status)
                                 .CountAsync();
        }
    }
}