using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubmissionBlockedTermRepository : ISubmissionBlockedTermRepository
    {
        private readonly IApplicationDbContext context;

        public SubmissionBlockedTermRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<SubmissionBlockedTerm>> GetAllAsync()
        {
            return await this.context.SubmissionBlockedTerms
                .OrderByDescending(x => x.IsEnabled)
                .ThenBy(x => x.Term)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<SubmissionBlockedTerm>> GetEnabledAsync()
        {
            return await this.context.SubmissionBlockedTerms
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }

        public async Task<SubmissionBlockedTerm?> CreateAsync(SubmissionBlockedTerm model)
        {
            if (string.IsNullOrWhiteSpace(model.Term))
            {
                return null;
            }

            model.Term = model.Term.Trim();

            var exists = await this.context.SubmissionBlockedTerms
                .AnyAsync(x => x.Term.ToLower() == model.Term.ToLower());

            if (exists)
            {
                return null;
            }

            this.context.SubmissionBlockedTerms.Add(model);
            await this.context.SaveChangesAsync();

            return model;
        }

        public async Task<bool> ToggleEnabledAsync(int id)
        {
            var existing = await this.context.SubmissionBlockedTerms
                .FirstOrDefaultAsync(x => x.SubmissionBlockedTermId == id);

            if (existing == null)
            {
                return false;
            }

            existing.IsEnabled = !existing.IsEnabled;
            await this.context.SaveChangesAsync();

            return existing.IsEnabled;
        }

        public async Task DeleteAsync(int id)
        {
            var existing = await this.context.SubmissionBlockedTerms
                .FirstOrDefaultAsync(x => x.SubmissionBlockedTermId == id);

            if (existing == null)
            {
                return;
            }

            this.context.SubmissionBlockedTerms.Remove(existing);
            await this.context.SaveChangesAsync();
        }
    }
}
