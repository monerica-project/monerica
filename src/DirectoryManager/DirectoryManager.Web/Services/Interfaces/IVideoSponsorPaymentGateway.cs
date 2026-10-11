using BtcPayServer.API.Interfaces;

namespace DirectoryManager.Web.Services.Interfaces
{
    // The BTCPay client for the dedicated video-sponsorship store. It is a normal
    // IBtcPayServerService bound to that store's id, API key and webhook secret, so every
    // existing call (create/get invoice, XMR payment method, webhook signature) works unchanged.
    public interface IVideoSponsorPaymentGateway
    {
        // False until the store id, API key and webhook secret are all present in the BTCPay config.
        bool IsConfigured { get; }

        // Null when not configured.
        IBtcPayServerService? Client { get; }
    }
}
