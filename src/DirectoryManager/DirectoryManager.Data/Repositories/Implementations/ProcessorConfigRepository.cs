using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ProcessorConfigRepository : IProcessorConfigRepository
    {
        private readonly IApplicationDbContext context;

        public ProcessorConfigRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<ProcessorConfig>> GetAllAsync()
        {
            return await this.context.ProcessorConfigs.ToListAsync();
        }

        public async Task<ProcessorConfig> GetByIdAsync(int id)
        {
            var result = await this.context.ProcessorConfigs.FindAsync(id);

            return result == null ?
                throw new KeyNotFoundException($"ProcessorConfig with id {id} not found") : result;
        }

        public async Task CreateAsync(ProcessorConfig processorConfigs)
        {
            this.context.ProcessorConfigs.Add(processorConfigs);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ProcessorConfig processorConfigs)
        {
            this.context.ProcessorConfigs.Update(processorConfigs);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var processorConfigs = await this.context.ProcessorConfigs.FindAsync(id);

            if (processorConfigs == null)
            {
                throw new KeyNotFoundException($"ProcessorConfig with id {id} not found");
            }

            this.context.ProcessorConfigs.Remove(processorConfigs);
            await this.context.SaveChangesAsync();
        }

        public async Task<ProcessorConfig> GetByProcessorAsync(PaymentProcessor paymentProcessor)
        {
             var result = await this.context.ProcessorConfigs.FirstOrDefaultAsync(x => x.PaymentProcessor == paymentProcessor);

             return result ?? throw new KeyNotFoundException($"ProcessorConfig with id {paymentProcessor} not found");
        }
    }
}
