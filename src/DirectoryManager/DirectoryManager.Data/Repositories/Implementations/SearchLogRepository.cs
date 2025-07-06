using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;


namespace DirectoryManager.Data.Repositories.Implementations
{
    /// <inheritdoc />
    public class SearchLogRepository : ISearchLogRepository
    {
        private readonly IApplicationDbContext context;

        public SearchLogRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc />
        public async Task CreateAsync(SearchLog log)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // stamp the time if not set
            log.CreateDate = log.CreateDate == default
                ? DateTime.UtcNow
                : log.CreateDate;

            await this.context.SearchLogs.AddAsync(log);
            await this.context.SaveChangesAsync();
        }
    }
}
