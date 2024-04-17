using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IProcessorConfigRepository
    {
        Task<IEnumerable<ProcessorConfig>> GetAllAsync();
        Task<ProcessorConfig> GetByIdAsync(int processorConfigId);
        Task<ProcessorConfig> GetByProcessorAsync(PaymentProcessor paymentProcessor);
        Task CreateAsync(ProcessorConfig processorConfigs);
        Task UpdateAsync(ProcessorConfig processorConfigs);
        Task DeleteAsync(int processorConfigId);
    }
}
