using BtcPayServer.API.Models;

namespace BtcPayServer.API.Interfaces
{
    public interface IBtcPayServerService
    {
        string DefaultCurrency { get; }
        string SuccessUrl { get; }
        string CancelUrl { get; }
        string ReviewRequestsStoreId { get; }

        // Base URL of the BTCPay instance (e.g. https://btcpayserver.monerica.com), from config.
        string BaseUrl { get; }

        Task<BtcPayInvoiceResponse> CreateInvoiceAsync(BtcPayInvoiceRequest request);
        Task<BtcPayInvoiceResponse> GetInvoiceAsync(string invoiceId);

        // Same server + (account-level) API key, but targeting an arbitrary store — used
        // for the separate "Monerica - ReviewRequests" donation store.
        Task<BtcPayInvoiceResponse> CreateInvoiceOnStoreAsync(string storeId, BtcPayInvoiceRequest request);
        Task<BtcPayInvoiceResponse> GetInvoiceOnStoreAsync(string storeId, string invoiceId);
        Task<BtcPayPaymentMethod?> GetXmrPaymentMethodOnStoreAsync(string storeId, string invoiceId);
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