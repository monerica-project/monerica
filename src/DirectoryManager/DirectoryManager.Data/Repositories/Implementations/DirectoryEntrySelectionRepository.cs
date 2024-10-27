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
            var result = await this.context.DirectoryEntrySelections.FindAsync(directoryEntrySelectionId);

            return result == null ?
                throw new InvalidOperationException($"No DirectoryEntrySelection found with ID {directoryEntrySelectionId}") :
                result;
        }

        public async Task AddToList(DirectoryEntrySelection selection)
        {
            this.context.DirectoryEntrySelections.Add(selection);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteFromList(int directoryEntrySelectionId)
        {
            var selection = await this.GetByID(directoryEntrySelectionId);
            this.context.DirectoryEntrySelections.Remove(selection);
            await this.context.SaveChangesAsync();
        }

        public IEnumerable<DirectoryEntrySelection> GetAll()
        {
            return this.context.DirectoryEntrySelections.Include(de => de.DirectoryEntry).ToList();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetEntriesForSelection(EntrySelectionType type)
        {
            var result = await this.context.DirectoryEntrySelections
                                 .Where(d => d.EntrySelectionType == type)
                                 .Select(d => d.DirectoryEntry)
                                 .Where(d => d != null)
                                 .Cast<DirectoryEntry>()
                                 .ToListAsync();

            return result;
        }

        public async Task<DateTime> GetMostRecentModifiedDateAsync()
        {
            var mostRecentDate = await this.context.DirectoryEntrySelections
                .Select(d => d.UpdateDate ?? d.CreateDate)  // Use CreateDate if UpdateDate is null
                .OrderByDescending(date => date)
                .FirstOrDefaultAsync();

            // Return the most recent date or DateTime.MinValue if no entries are found
            return mostRecentDate == default(DateTime) ? DateTime.MinValue : mostRecentDate;
        }
    }
}