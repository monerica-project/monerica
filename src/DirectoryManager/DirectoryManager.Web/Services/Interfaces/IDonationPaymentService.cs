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

        /// <summary>
        /// Stores the XMR actually received for an already-paid donation (backfill for donations
        /// recorded before the XMR amount was kept). Returns true when it was stored.
        /// </summary>
        Task<bool> RecordPaidXmrAsync(string invoiceId, CancellationToken ct = default);
    }
}
