namespace DirectoryManager.Web.Services.Interfaces
{
    public enum LatePaymentOutcome
    {
        /// <summary>Nothing to do for this invoice.</summary>
        NotApplicable = 0,

        /// <summary>The invoice is now paid and its sponsorship is live.</summary>
        Accepted = 1,

        /// <summary>Paid late in full; waiting for the payment to confirm. Checked again shortly.</summary>
        WaitingForConfirmations = 2,

        /// <summary>Another buyer is in checkout for the slot right now. Checked again shortly.</summary>
        SlotHeld = 3,

        /// <summary>The slot was sold to someone else meanwhile: the owner decides (refund or wait).</summary>
        SlotTaken = 4,

        /// <summary>Paid later than the grace period: the owner decides.</summary>
        TooLate = 5,
    }

    /// <summary>
    /// Accepts sponsorship payments that reached BTCPay after the invoice timer ran out, so the
    /// listing goes live without the owner marking the invoice settled by hand.
    /// </summary>
    public interface ISponsorLatePaymentService
    {
        /// <summary>Checks one invoice against BTCPay and activates it when the rules allow. Safe to call repeatedly.</summary>
        /// <returns>What was done.</returns>
        Task<LatePaymentOutcome> TryAcceptAsync(int sponsoredListingInvoiceId, CancellationToken ct = default);
    }
}
