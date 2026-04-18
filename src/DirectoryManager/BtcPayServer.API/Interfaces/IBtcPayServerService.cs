using BtcPayServer.API.Models;

namespace BtcPayServer.API.Interfaces
{
    public interface IBtcPayServerService
    {
        string DefaultCurrency { get; }
        string SuccessUrl { get; }
        string CancelUrl { get; }

        Task<BtcPayInvoiceResponse> CreateInvoiceAsync(BtcPayInvoiceRequest request);
        Task<BtcPayInvoiceResponse> GetInvoiceAsync(string invoiceId);
        Task<BtcPayPaymentMethod?> GetXmrPaymentMethodAsync(string invoiceId);

        Task<BtcPayStoreRateResponse?> GetStoreRateAsync(
            string baseCurrency,
            string quoteCurrency);

        Task<decimal> GetXmrRateAsync(string quoteCurrency = "USD");

        bool IsWebhookValid(
            string requestBody,
            string btcPaySigHeader,
            out string errorMsg);
    }
}