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

        public async Task<DirectoryEntrySelection> GetByID(int directoryEntrySelectionId)
        {
            var result = await this.SelectionBaseQuery()
                .FirstOrDefaultAsync(s => s.DirectoryEntrySelectionId == directoryEntrySelectionId)
                .ConfigureAwait(false);

            return result == null
                ? throw new InvalidOperationException($"No DirectoryEntrySelection found with ID {directoryEntrySelectionId}")
                : result;
        }

        public async Task AddToList(DirectoryEntrySelection selection)
        {
            this.context.DirectoryEntrySelections.Add(selection);
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task DeleteFromList(int directoryEntrySelectionId)
        {
            var selection = await this.GetByID(directoryEntrySelectionId).ConfigureAwait(false);
            this.context.DirectoryEntrySelections.Remove(selection);
            await this.context.SaveChangesAsync().ConfigureAwait(false);
        }

        public IEnumerable<DirectoryEntrySelection> GetAll()
        {
            // If you prefer async, make this Task<IEnumerable<...>> and await ToListAsync.
            return this.SelectionBaseQuery()
                .AsNoTracking()
                .ToList();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetEntriesForSelection(EntrySelectionType type)
        {
            // Return DirectoryEntry list WITH SubCategory → Category eagerly loaded.
            // Use the DirectoryEntries set as the root to ensure Includes are honored.
            var entries = await this.context.DirectoryEntries
                .Include(e => e.SubCategory!)
                    .ThenInclude(sc => sc.Category!)
                .Where(e => this.context.DirectoryEntrySelections
                    .Any(s => s.EntrySelectionType == type && s.DirectoryEntryId == e.DirectoryEntryId))
                .ToListAsync()
                .ConfigureAwait(false);

            return entries;
        }

        public async Task<DateTime> GetMostRecentModifiedDateAsync()
        {
            var mostRecentDate = await this.context.DirectoryEntrySelections
                .Select(d => d.UpdateDate ?? d.CreateDate)
                .OrderByDescending(date => date)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return mostRecentDate == default ? DateTime.MinValue : mostRecentDate;
        }

        private IQueryable<DirectoryEntrySelection> SelectionBaseQuery()
        {
            return this.context.DirectoryEntrySelections
                .Include(s => s.DirectoryEntry!)
                    .ThenInclude(de => de.SubCategory!)
                        .ThenInclude(sc => sc.Category!);
        }
    }
}