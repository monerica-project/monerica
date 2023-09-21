using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IExcludeUserAgentRepository
    {
        IEnumerable<ExcludeUserAgent> GetAll();
        void Create(ExcludeUserAgent excludeUserAgent);
        bool Exists(string userAgent);
    }
}