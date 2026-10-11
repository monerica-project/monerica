using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VideoSponsorships;

namespace DirectoryManager.Web.Services.Interfaces
{
    // Applies payment-status changes to a video-sponsor order and the side effects that go
    // with them (video holds, assigning videos once paid). Shared by the BTCPay webhook, the
    // buyer's invoice page and the admin so all three behave identically.
    public interface IVideoSponsorOrderProcessor
    {
        // Moves the order to the proposed status when that is a real step forward.
        Task ApplyStatusAsync(VideoSponsorOrder order, PaymentStatus proposed, string? paymentResponseJson = null, bool force = false, CancellationToken ct = default);

        // Reads the invoice from BTCPay and applies whatever status it reports. Best-effort.
        Task SyncFromProcessorAsync(VideoSponsorOrder order, CancellationToken ct = default);
    }
}
