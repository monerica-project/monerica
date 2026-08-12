namespace DirectoryManager.Web.Models.VerificationRequests
{
    // Backs the optional "cover our review costs" page at /verification-requests/pay/{token}.
    public class VerificationPayViewModel
    {
        public Guid Token { get; set; }

        public string EntryName { get; set; } = string.Empty;

        // Show the swap-fees note only for exchange/swap listings.
        public bool IsExchange { get; set; }

        // A payment was already detected for this request.
        public bool AlreadyPaid { get; set; }
    }
}
