using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IProcessorConfigRepository
    {
        Task<IEnumerable<ProcessorConfig>> GetAllAsync();
        Task<ProcessorConfig> GetByIdAsync(int id);
        Task<ProcessorConfig> GetByProcessorAsync(PaymentProcessor paymentProcessor);
        Task CreateAsync(ProcessorConfig processorConfigs);
        Task UpdateAsync(ProcessorConfig processorConfigs);
        Task DeleteAsync(int id);
    }
}
