using DirectoryManager.Common.Models;

namespace DirectoryManager.Common.Interfaces
{
    public interface IRateConversionService
    {
        Task<ConversionEstimate> GetEstimatedConversionAsync(
            decimal amount,
            string fromCurrency,
            string toCurrency);
    }
}