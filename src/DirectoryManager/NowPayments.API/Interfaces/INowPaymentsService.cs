using NowPayments.API.Models;

namespace NowPayments.API.Interfaces
{
    public interface INowPaymentsService
    {
        Task<InvoiceResponse> CreateInvoice(PaymentRequest request);

        Task<PaymentStatusResponse> GetPaymentStatusAsync(string paymentId);

        bool IsIpnRequestValid(string requestBody, string paymentSignature, out string errorMsg);
    }
}