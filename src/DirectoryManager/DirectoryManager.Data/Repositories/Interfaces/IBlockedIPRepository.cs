using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IBlockedIPRepository : IDisposable
    {
        IApplicationDbContext Context { get; }

        Task<BlockedIP> CreateAsync(BlockedIP model);

        bool IsBlockedIp(string ipAddress);
    }
}
