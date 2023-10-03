﻿using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IDirectoryEntrySelectionRepository
    {
        Task<DirectoryEntrySelection> GetByID(int id);
        Task AddToList(DirectoryEntrySelection selection);
        Task DeleteFromList(int id);
        IEnumerable<DirectoryEntrySelection> GetAll();
        Task<IEnumerable<DirectoryEntry>> GetEntriesForSelection(EntrySelectionType type);
    }
}