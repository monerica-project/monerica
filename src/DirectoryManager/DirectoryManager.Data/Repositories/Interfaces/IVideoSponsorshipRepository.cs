using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VideoSponsorships;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IVideoSponsorshipRepository
    {
        // ---- Offers (video types + prices) ----
        Task<List<VideoSponsorOffer>> GetOffersAsync(bool enabledOnly, CancellationToken ct = default);

        Task<VideoSponsorOffer?> GetOfferAsync(int offerId, CancellationToken ct = default);

        Task AddOfferAsync(VideoSponsorOffer offer, CancellationToken ct = default);

        Task UpdateOfferAsync(VideoSponsorOffer offer, CancellationToken ct = default);

        // ---- Video queue ----
        Task<List<VideoIdea>> GetIdeasAsync(VideoIdeaStatus? status = null, CancellationToken ct = default);

        Task<VideoIdea?> GetIdeaAsync(int videoIdeaId, CancellationToken ct = default);

        // Queued, not dedicated, not sponsored, and not held by another buyer's open invoice.
        // True when another (non-dedicated) video idea already uses this topic, ignoring case and outer spaces.
        Task<bool> IdeaTopicExistsAsync(string topic, int excludeVideoIdeaId, CancellationToken ct = default);

        Task<List<VideoIdea>> GetAvailableIdeasAsync(DateTime nowUtc, CancellationToken ct = default);

        Task AddIdeaAsync(VideoIdea idea, CancellationToken ct = default);

        Task UpdateIdeaAsync(VideoIdea idea, CancellationToken ct = default);

        // ---- Checkout holds (one sponsor per video) ----

        // Atomically locks every requested video for the order. All-or-nothing: returns false
        // (and holds nothing) if any of them is no longer available.
        Task<bool> TryHoldIdeasAsync(int orderId, IReadOnlyCollection<int> videoIdeaIds, DateTime heldUntilUtc, DateTime nowUtc, CancellationToken ct = default);

        Task SetHoldExpiryAsync(int orderId, DateTime heldUntilUtc, CancellationToken ct = default);

        Task ReleaseHoldsAsync(int orderId, CancellationToken ct = default);

        // ---- Orders ----
        Task AddOrderAsync(VideoSponsorOrder order, CancellationToken ct = default);

        Task UpdateOrderAsync(VideoSponsorOrder order, CancellationToken ct = default);

        Task<VideoSponsorOrder?> GetOrderAsync(int orderId, CancellationToken ct = default);

        Task<VideoSponsorOrder?> GetOrderByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);

        Task<VideoSponsorOrder?> GetOrderByProcessorInvoiceIdAsync(string processorInvoiceId, CancellationToken ct = default);

        Task<List<VideoSponsorOrder>> ListOrdersAsync(CancellationToken ct = default);

        // Paid (or overpaid) orders only, newest payment first.
        Task<List<VideoSponsorOrder>> ListPaidOrdersAsync(CancellationToken ct = default);

        Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default);

        // Assigns each video on a just-paid order to it. A dedicated order gets its own queue
        // entry created here. Returns false when one of the videos was already sold to another
        // order (the caller flags the conflict for the admin).
        Task<bool> AssignVideosToPaidOrderAsync(int orderId, string dedicatedTitle, CancellationToken ct = default);

        Task<VideoSponsorOrderItem?> GetItemAsync(int orderItemId, CancellationToken ct = default);

        Task UpdateItemAsync(VideoSponsorOrderItem item, CancellationToken ct = default);
    }
}
