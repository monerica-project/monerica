﻿using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    // ITagRepository.cs
    public interface ITagRepository
    {
        Task<Tag?> GetByIdAsync(int tagId);
        Task<Tag?> GetByKeyAsync(string name);
        Task<IReadOnlyList<Tag>> ListAllAsync();
        Task<Tag> CreateAsync(string name);      // existing
        Task DeleteAsync(int tagId);
        /// <summary>
        /// Returns only those tags which are attached to at least one non-removed entry,
        /// along with the most recent Create/Update date among those entries.
        /// </summary>
        Task<IReadOnlyList<TagWithLastModified>> ListActiveTagsWithLastModifiedAsync();

        // in ITagRepository
        Task<PagedResult<TagCount>> ListTagsWithCountsPagedAsync(int page, int pageSize);
    }
}
