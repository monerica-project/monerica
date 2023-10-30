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

        public async Task<Submission?> GetByIdAsync(int id)
        {
            return await this.context.Submissions.FindAsync(id);
        }

        public async Task AddAsync(Submission submission)
        {
            await this.context.Submissions.AddAsync(submission);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Submission submission)
        {
            this.context.Submissions.Update(submission);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var submission = await this.GetByIdAsync(id);
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