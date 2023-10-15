using NowPayments.API.Models;

namespace NowPayments.API.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentResponse> CreateInvoice(PaymentRequest request);

        bool IsIpnRequestValid(string requestBody, string paymentSignature, out string errorMsg);
    }
}