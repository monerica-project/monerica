using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VideoSponsorships;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class VideoSponsorshipRepository : IVideoSponsorshipRepository
    {
        private readonly IApplicationDbContext context;

        public VideoSponsorshipRepository(IApplicationDbContext context) => this.context = context;

        // NOTE: the Update* methods save the TRACKED entity returned by the Get* methods (changed
        // columns only). They deliberately avoid DbSet.Update(), which would rewrite the whole
        // loaded graph and could clobber hold/sponsor columns set by the atomic bulk updates.

        // ---- Offers ----
        public async Task<List<VideoSponsorOffer>> GetOffersAsync(bool enabledOnly, CancellationToken ct = default) =>
            await this.context.VideoSponsorOffers.AsNoTracking()
                .Where(x => !enabledOnly || x.IsEnabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.VideoSponsorOfferId)
                .ToListAsync(ct);

        public async Task<VideoSponsorOffer?> GetOfferAsync(int offerId, CancellationToken ct = default) =>
            await this.context.VideoSponsorOffers.FirstOrDefaultAsync(x => x.VideoSponsorOfferId == offerId, ct);

        public async Task AddOfferAsync(VideoSponsorOffer offer, CancellationToken ct = default)
        {
            offer.CreateDate = DateTime.UtcNow;
            this.context.VideoSponsorOffers.Add(offer);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateOfferAsync(VideoSponsorOffer offer, CancellationToken ct = default)
        {
            offer.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        // ---- Video queue ----
        public async Task<List<VideoIdea>> GetIdeasAsync(VideoIdeaStatus? status = null, CancellationToken ct = default) =>
            await this.context.VideoIdeas.AsNoTracking()
                .Where(x => status == null || x.Status == status)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.VideoIdeaId)
                .ToListAsync(ct);

        public async Task<VideoIdea?> GetIdeaAsync(int videoIdeaId, CancellationToken ct = default) =>
            await this.context.VideoIdeas.FirstOrDefaultAsync(x => x.VideoIdeaId == videoIdeaId, ct);

        public async Task<bool> IdeaTopicExistsAsync(string topic, int excludeVideoIdeaId, CancellationToken ct = default)
        {
            var normalized = (topic ?? string.Empty).Trim().ToLower();
            return await this.context.VideoIdeas.AsNoTracking()
                .AnyAsync(
                    x => !x.IsDedicated
                        && x.VideoIdeaId != excludeVideoIdeaId
                        && x.Title.Trim().ToLower() == normalized,
                    ct);
        }

        public async Task<List<VideoIdea>> GetAvailableIdeasAsync(DateTime nowUtc, CancellationToken ct = default) =>
            await this.context.VideoIdeas.AsNoTracking()
                .Where(x => x.Status == VideoIdeaStatus.Queued
                    && !x.IsDedicated
                    && x.SponsoredByOrderId == null
                    && (x.HeldUntilUtc == null || x.HeldUntilUtc < nowUtc))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.VideoIdeaId)
                .ToListAsync(ct);

        public async Task AddIdeaAsync(VideoIdea idea, CancellationToken ct = default)
        {
            idea.CreateDate = DateTime.UtcNow;
            this.context.VideoIdeas.Add(idea);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateIdeaAsync(VideoIdea idea, CancellationToken ct = default)
        {
            idea.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        // ---- Checkout holds ----
        public async Task<bool> TryHoldIdeasAsync(
            int orderId, IReadOnlyCollection<int> videoIdeaIds, DateTime heldUntilUtc, DateTime nowUtc, CancellationToken ct = default)
        {
            var ids = videoIdeaIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return true;
            }

            // One conditional UPDATE: the database row locks make this race-free, so two buyers
            // can never both end up holding the same video.
            var held = await this.context.VideoIdeas
                .Where(x => ids.Contains(x.VideoIdeaId)
                    && x.Status == VideoIdeaStatus.Queued
                    && !x.IsDedicated
                    && x.SponsoredByOrderId == null
                    && (x.HeldUntilUtc == null || x.HeldUntilUtc < nowUtc || x.HeldByOrderId == orderId))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.HeldByOrderId, orderId)
                          .SetProperty(x => x.HeldUntilUtc, heldUntilUtc),
                    ct);

            if (held == ids.Count)
            {
                return true;
            }

            // Got only some of them: give those back so nothing stays half-held.
            await this.ReleaseHoldsAsync(orderId, ct);
            return false;
        }

        public async Task SetHoldExpiryAsync(int orderId, DateTime heldUntilUtc, CancellationToken ct = default) =>
            await this.context.VideoIdeas
                .Where(x => x.HeldByOrderId == orderId && x.SponsoredByOrderId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeldUntilUtc, heldUntilUtc), ct);

        public async Task ReleaseHoldsAsync(int orderId, CancellationToken ct = default) =>
            await this.context.VideoIdeas
                .Where(x => x.HeldByOrderId == orderId && x.SponsoredByOrderId == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.HeldByOrderId, (int?)null)
                          .SetProperty(x => x.HeldUntilUtc, (DateTime?)null),
                    ct);

        // ---- Orders ----
        public async Task AddOrderAsync(VideoSponsorOrder order, CancellationToken ct = default)
        {
            order.CreateDate = DateTime.UtcNow;
            this.context.VideoSponsorOrders.Add(order);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateOrderAsync(VideoSponsorOrder order, CancellationToken ct = default)
        {
            order.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }

        public async Task<VideoSponsorOrder?> GetOrderAsync(int orderId, CancellationToken ct = default) =>
            await this.OrdersWithDetails().FirstOrDefaultAsync(x => x.VideoSponsorOrderId == orderId, ct);

        public async Task<VideoSponsorOrder?> GetOrderByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default) =>
            await this.OrdersWithDetails().FirstOrDefaultAsync(x => x.InvoiceId == invoiceId, ct);

        public async Task<VideoSponsorOrder?> GetOrderByProcessorInvoiceIdAsync(string processorInvoiceId, CancellationToken ct = default) =>
            string.IsNullOrWhiteSpace(processorInvoiceId)
                ? null
                : await this.OrdersWithDetails().FirstOrDefaultAsync(x => x.ProcessorInvoiceId == processorInvoiceId, ct);

        public async Task<List<VideoSponsorOrder>> ListOrdersAsync(CancellationToken ct = default) =>
            await this.OrdersWithDetails().AsNoTracking()
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.VideoSponsorOrderId)
                .ToListAsync(ct);

        public async Task<List<VideoSponsorOrder>> ListPaidOrdersAsync(CancellationToken ct = default) =>
            await this.OrdersWithDetails().AsNoTracking()
                .Where(x => x.PaymentStatus == PaymentStatus.Paid || x.PaymentStatus == PaymentStatus.OverPayment)
                .OrderByDescending(x => x.PaidUtc)
                .ThenByDescending(x => x.VideoSponsorOrderId)
                .ToListAsync(ct);

        public async Task<DateTime?> GetLastPaidUtcAsync(CancellationToken ct = default) =>
            await this.context.VideoSponsorOrders.AsNoTracking()
                .Where(x => x.PaidUtc != null
                    && (x.PaymentStatus == PaymentStatus.Paid || x.PaymentStatus == PaymentStatus.OverPayment))
                .MaxAsync(x => x.PaidUtc, ct);

        public async Task<bool> AssignVideosToPaidOrderAsync(int orderId, string dedicatedTitle, CancellationToken ct = default)
        {
            var items = await this.context.VideoSponsorOrderItems
                .Where(x => x.VideoSponsorOrderId == orderId)
                .ToListAsync(ct);

            var allAssigned = true;
            foreach (var item in items)
            {
                if (item.VideoIdeaId is int ideaId)
                {
                    // Claim it unless another order got there first (only possible if this
                    // order's hold had already lapsed when the payment arrived).
                    var claimed = await this.context.VideoIdeas
                        .Where(x => x.VideoIdeaId == ideaId
                            && (x.SponsoredByOrderId == null || x.SponsoredByOrderId == orderId))
                        .ExecuteUpdateAsync(
                            s => s.SetProperty(x => x.SponsoredByOrderId, orderId)
                                  .SetProperty(x => x.HeldByOrderId, (int?)null)
                                  .SetProperty(x => x.HeldUntilUtc, (DateTime?)null),
                            ct);

                    if (claimed == 0)
                    {
                        allAssigned = false;
                    }

                    continue;
                }

                // Dedicated video: give it its own entry in the queue.
                var idea = new VideoIdea
                {
                    Title = dedicatedTitle,
                    Status = VideoIdeaStatus.Queued,
                    IsDedicated = true,
                    SponsoredByOrderId = orderId,
                    SortOrder = 0,
                    CreateDate = DateTime.UtcNow,
                };
                this.context.VideoIdeas.Add(idea);
                await this.context.SaveChangesAsync(ct);

                item.VideoIdeaId = idea.VideoIdeaId;
                item.Title = dedicatedTitle;
                await this.context.SaveChangesAsync(ct);
            }

            return allAssigned;
        }

        public async Task<VideoSponsorOrderItem?> GetItemAsync(int orderItemId, CancellationToken ct = default) =>
            await this.context.VideoSponsorOrderItems
                .Include(x => x.VideoIdea)
                .Include(x => x.VideoSponsorOrder).ThenInclude(o => o.DirectoryEntry)
                .FirstOrDefaultAsync(x => x.VideoSponsorOrderItemId == orderItemId, ct);

        public async Task UpdateItemAsync(VideoSponsorOrderItem item, CancellationToken ct = default)
        {
            await this.context.SaveChangesAsync(ct);
        }

        private IQueryable<VideoSponsorOrder> OrdersWithDetails() =>
            this.context.VideoSponsorOrders
                .Include(x => x.DirectoryEntry)
                .Include(x => x.VideoSponsorOffer)
                .Include(x => x.Items).ThenInclude(i => i.VideoIdea);
    }
}
