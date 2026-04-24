using DirectoryManager.Common.Interfaces;
using DirectoryManager.Common.Models;
using NowPayments.API.Interfaces;

namespace NowPayments.API.Implementations
{
    public class NowPaymentsRateConversionAdapter : IRateConversionService
    {
        private readonly INowPaymentsService nowPayments;

        public NowPaymentsRateConversionAdapter(INowPaymentsService nowPayments)
        {
            this.nowPayments = nowPayments
                ?? throw new ArgumentNullException(nameof(nowPayments));
        }

        public async Task<ConversionEstimate> GetEstimatedConversionAsync(
            decimal amount,
            string fromCurrency,
            string toCurrency)
        {
            var result = await this.nowPayments
                .GetEstimatedConversionAsync(amount, fromCurrency, toCurrency)
                .ConfigureAwait(false);

            return new ConversionEstimate
            {
                EstimatedAmount = result.EstimatedAmount,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
            };
        }
    }
}