using DirectoryManager.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntryRepository
    {
        Task<DirectoryEntry> GetByIdAsync(int id);
        Task<DirectoryEntry> GetByLinkAsync(string link);
        Task<IEnumerable<DirectoryEntry>> GetAllAsync();
        Task CreateAsync(DirectoryEntry entry);
        Task UpdateAsync(DirectoryEntry entry);
        Task DeleteAsync(int id);
    }

}
