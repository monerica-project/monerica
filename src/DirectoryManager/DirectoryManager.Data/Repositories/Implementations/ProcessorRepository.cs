using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ProcessorRepository : IProcessorRepository
    {
        private readonly IApplicationDbContext context;

        public ProcessorRepository(IApplicationDbContext context)
            => this.context = context;

        private DbSet<Processor> Set => this.context.Processors;

        public Task<Processor?> GetByIdAsync(int id, CancellationToken ct = default)
            => this.Set.AsNoTracking().FirstOrDefaultAsync(x => x.ProcessorId == id, ct);

        public Task<Processor?> GetByNameAsync(string name, CancellationToken ct = default)
        {
            name = (name ?? string.Empty).Trim();
            return this.Set.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), ct);
        }

        public Task<List<Processor>> ListAllAsync(CancellationToken ct = default)
            => this.Set.AsNoTracking()
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ProcessorId)
                .ToListAsync(ct);

        public async Task<Processor> CreateAsync(Processor entity, CancellationToken ct = default)
        {
            if (entity is null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            entity.Name = (entity.Name ?? string.Empty).Trim();
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;

            try
            {
                this.Set.Add(entity);
                await this.context.SaveChangesAsync(ct).ConfigureAwait(false);
                return entity;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex);
            }
        }

        public async Task UpdateAsync(Processor entity, CancellationToken ct = default)
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));

            var existing = await this.Set
                .FirstOrDefaultAsync(x => x.ProcessorId == entity.ProcessorId, ct)
                .ConfigureAwait(false);

            if (existing is null) return;

            existing.Name = (entity.Name ?? string.Empty).Trim();
            existing.UpdatedByUserId = entity.UpdatedByUserId;
            existing.UpdateDate = DateTime.UtcNow;

            try
            {
                await this.context.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex);
            }
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await this.Set.FirstOrDefaultAsync(x => x.ProcessorId == id, ct)
                .ConfigureAwait(false);

            if (existing is null) return;

            try
            {
                this.Set.Remove(existing);
                await this.context.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex);
            }
        }

        public async Task<List<IdNameOption>> ListOptionsAsync(CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ProcessorId)
                .Select(x => new IdNameOption { Id = x.ProcessorId, Name = x.Name })
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
    }
}