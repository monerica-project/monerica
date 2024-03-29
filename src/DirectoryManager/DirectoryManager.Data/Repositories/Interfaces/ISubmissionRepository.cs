﻿using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubmissionRepository
    {
        Task<IEnumerable<Submission>> GetAllAsync();
        Task<Submission?> GetByIdAsync(int id);
        Task<Submission> CreateAsync(Submission submission);
        Task UpdateAsync(Submission submission);
        Task DeleteAsync(int id);
        Task<int> GetByStatus(SubmissionStatus pending);
    }
}