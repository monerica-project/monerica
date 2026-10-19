namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>Records donation payments (submissions, verification requests, problem reports) reported by the BTCPay webhook.</summary>
    public interface IDonationPaymentService
    {
        /// <summary>
        /// Marks whichever record owns this donations-store invoice as paid, if BTCPay has seen
        /// a payment for it. Returns false when the invoice isn't one of ours or is already paid.
        /// </summary>
        Task<bool> RecordPaymentAsync(string invoiceId, CancellationToken ct = default);
    }
}
