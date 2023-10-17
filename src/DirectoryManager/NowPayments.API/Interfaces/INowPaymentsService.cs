﻿using NowPayments.API.Models;

namespace NowPayments.API.Interfaces
{
    public interface INowPaymentsService
    {
        void SetDefaultUrls(PaymentRequest request);

        Task<InvoiceResponse> CreateInvoice(PaymentRequest request);

        Task<PaymentResponse> CreatePaymentAsync(PaymentCreationRequest paymentRequest);

        Task<PaymentStatusResponse> GetPaymentStatusAsync(string paymentId);

        bool IsIpnRequestValid(string requestBody, string paymentSignature, out string errorMsg);
    }
}