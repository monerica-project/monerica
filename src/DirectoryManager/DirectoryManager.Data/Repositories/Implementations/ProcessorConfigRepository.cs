using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
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

        public async Task<ProcessorConfig> GetByIdAsync(int processorConfigId)
        {
            var result = await this.context.ProcessorConfigs.FindAsync(processorConfigId);

            return result ?? throw new KeyNotFoundException($"ProcessorConfig with id {processorConfigId} not found");
        }

        public async Task CreateAsync(ProcessorConfig processorConfigs)
        {
            if (processorConfigs.UseProcessor)
            {
                var configs = this.context.ProcessorConfigs.Where(p => p.UseProcessor);
                foreach (var config in configs)
                {
                    config.UseProcessor = false;
                }
            }

            this.context.ProcessorConfigs.Add(processorConfigs);
            await this.context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ProcessorConfig processorConfigs)
        {
            if (processorConfigs.UseProcessor)
            {
                var configs = this.context
                                  .ProcessorConfigs
                                  .Where(p => p.UseProcessor && p.ProcessorConfigId != processorConfigs.ProcessorConfigId);

                foreach (var config in configs)
                {
                    config.UseProcessor = false;
                }
            }

            this.context.ProcessorConfigs.Update(processorConfigs);
            await this.context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int processorConfigId)
        {
            var processorConfigs = await this.context.ProcessorConfigs.FindAsync(processorConfigId);

            if (processorConfigs == null)
            {
                throw new KeyNotFoundException($"ProcessorConfig with id {processorConfigId} not found");
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
