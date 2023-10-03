using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntrySelectionRepository : IDirectoryEntrySelectionRepository
    {
        private readonly IApplicationDbContext context;

        public DirectoryEntrySelectionRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<DirectoryEntrySelection> GetByID(int id)
        {
            return await this.context.DirectoryEntrySelections.FindAsync(id);
        }

        public async Task AddToList(DirectoryEntrySelection selection)
        {
            this.context.DirectoryEntrySelections.Add(selection);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteFromList(int id)
        {
            var selection = await this.GetByID(id);
            this.context.DirectoryEntrySelections.Remove(selection);
            await this.context.SaveChangesAsync();
        }

        public IEnumerable<DirectoryEntrySelection> GetAll()
        {
            return this.context.DirectoryEntrySelections.Include(de => de.DirectoryEntry).ToList();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllByType(EntrySelectionType type)
        {
            var result = await this.context.DirectoryEntrySelections
                             .Where(d => d.EntrySelectionType == type)
                             .Select(d => d.DirectoryEntry)
                             .ToListAsync();

            if (result == null)
            {
                return new List<DirectoryEntry>();
            }

            return result;
        }
    }
}