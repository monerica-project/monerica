using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SubmissionBlockedDomainRepository : ISubmissionBlockedDomainRepository
    {
        private readonly IApplicationDbContext context;

        public SubmissionBlockedDomainRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<SubmissionBlockedDomain>> GetAllAsync()
        {
            return await this.context.SubmissionBlockedDomains
                .OrderByDescending(x => x.IsEnabled)
                .ThenBy(x => x.Domain)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<SubmissionBlockedDomain>> GetEnabledAsync()
        {
            return await this.context.SubmissionBlockedDomains
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }

        public async Task<SubmissionBlockedDomain?> CreateAsync(SubmissionBlockedDomain model)
        {
            var domain = UrlHelper.NormalizeDomain(model.Domain);
            if (domain == null)
            {
                return null;
            }

            if (await this.context.SubmissionBlockedDomains.AnyAsync(x => x.Domain == domain))
            {
                return null;
            }

            model.Domain = domain;
            model.Message = string.IsNullOrWhiteSpace(model.Message) ? null : model.Message.Trim();
            this.context.SubmissionBlockedDomains.Add(model);
            await this.context.SaveChangesAsync();

            return model;
        }

        public async Task<bool> ToggleEnabledAsync(int id)
        {
            var existing = await this.context.SubmissionBlockedDomains
                .FirstOrDefaultAsync(x => x.SubmissionBlockedDomainId == id);

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
            var existing = await this.context.SubmissionBlockedDomains
                .FirstOrDefaultAsync(x => x.SubmissionBlockedDomainId == id);

            if (existing == null)
            {
                return;
            }

            this.context.SubmissionBlockedDomains.Remove(existing);
            await this.context.SaveChangesAsync();
        }
    }
}
