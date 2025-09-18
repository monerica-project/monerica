using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SearchBlacklistRepository : ISearchBlacklistRepository
    {
        private readonly IApplicationDbContext context;

        public SearchBlacklistRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task CreateAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("term is required", nameof(term));
            }

            term = term.Trim();

            bool exists = await this.context.SearchBlacklistTerms
                .AnyAsync(x => x.Term.ToLower() == term.ToLower());
            if (exists)
            {
                return;
            }

            await this.context.SearchBlacklistTerms.AddAsync(new SearchBlacklistTerm
            {
                Term = term,
                CreateDate = DateTime.UtcNow
            });

            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await this.context.SearchBlacklistTerms
                .FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return;
            }

            this.context.SearchBlacklistTerms.Remove(entity);
            await this.context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(string term)
        {
            term = term?.Trim() ?? "";
            if (term.Length == 0) return false;
            return await this.context.SearchBlacklistTerms
                .AnyAsync(x => x.Term.ToLower() == term.ToLower());
        }

        public async Task<IReadOnlyList<string>> GetAllTermsAsync()
        {
            return await this.context.SearchBlacklistTerms
                .OrderBy(x => x.Term)
                .Select(x => x.Term)
                .ToListAsync();
        }

        public async Task<int> CountAsync()
        {
            return await this.context.SearchBlacklistTerms.CountAsync();
        }

        public async Task<List<SearchBlacklistTerm>> ListPageAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            int skip = (page - 1) * pageSize;
            return await this.context.SearchBlacklistTerms
                .OrderBy(x => x.Term)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
