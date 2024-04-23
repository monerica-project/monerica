using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class BlockedIPRepository : IBlockedIPRepository
    {
        public BlockedIPRepository(IApplicationDbContext context)
        {
            this.Context = context;
        }

        public IApplicationDbContext Context { get; private set; }

        public async Task<BlockedIP> CreateAsync(BlockedIP model)
        {
            try
            {
                this.Context.BlockedIPs.Add(model);
                await this.Context.SaveChangesAsync();

                return model;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public async Task<IEnumerable<BlockedIP>> GetAllAsync()
        {
            return await this.Context.BlockedIPs.ToListAsync();
        }

        public void Dispose()
        {
            this.Context.Dispose();
        }

        public bool IsBlockedIp(string ipAddress)
        {
            try
            {
                var result = this.Context.BlockedIPs.FirstOrDefault(x => x.IpAddress == ipAddress);

                return result != null;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public async Task DeleteAsync(int id)
        {
            var blockedIP = await this.Context.BlockedIPs.FindAsync(id);
            if (blockedIP != null)
            {
                this.Context.BlockedIPs.Remove(blockedIP);
                await this.Context.SaveChangesAsync();
            }
            else
            {
                throw new Exception("Blocked IP not found");
            }
        }
    }
}