using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ExcludeUserAgentRepository : IExcludeUserAgentRepository
    {
        private readonly IApplicationDbContext context;

        public ExcludeUserAgentRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public IEnumerable<ExcludeUserAgent> GetAll()
        {
            return new List<ExcludeUserAgent>();
            //            return this.context.ExcludeUserAgents.ToList();
        }

        public void Create(ExcludeUserAgent excludeUserAgent)
        {
            this.context.ExcludeUserAgents.Add(excludeUserAgent);
            this.context.SaveChanges();
        }

        public bool Exists(string userAgent)
        {
            return this.context.ExcludeUserAgents.Any(e => e.UserAgent == userAgent);
        }
    }
}