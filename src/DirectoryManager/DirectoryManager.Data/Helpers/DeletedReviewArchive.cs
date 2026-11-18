using System.Text.Json;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Builds the admin-only <see cref="DeletedReviewRecord"/> for a review or reply that is about
    /// to be deleted, and rebuilds a review from one. The record is added to the context without
    /// saving, so it commits together with the delete.
    /// </summary>
    public static class DeletedReviewArchive
    {
        public sealed class ReplyCopy
        {
            public int Id { get; set; }

            public int? ParentCommentId { get; set; }

            public string Body { get; set; } = string.Empty;

            public ReviewModerationStatus ModerationStatus { get; set; }

            public string AuthorFingerprint { get; set; } = string.Empty;

            public string? AuthorUserId { get; set; }

            public string? AuthorUsername { get; set; }

            public string? DisplayName { get; set; }

            public DateTime CreateDate { get; set; }

            public DateTime? UpdateDate { get; set; }

            public string CreatedByUserId { get; set; } = string.Empty;
        }

        public sealed class ReviewCopy
        {
            public int DirectoryEntryId { get; set; }

            public string AuthorFingerprint { get; set; } = string.Empty;

            public string? AuthorPublicKeyArmor { get; set; }

            public string? AuthorUserId { get; set; }

            public string? AuthorUsername { get; set; }

            public string? AuthorHandle { get; set; }

            public string? DisplayName { get; set; }

            public string? DisplayNameSignatureArmor { get; set; }

            public byte? Rating { get; set; }

            public string? Title { get; set; }

            public string Body { get; set; } = string.Empty;

            public string RejectionReason { get; set; } = string.Empty;

            public ReviewModerationStatus ModerationStatus { get; set; }

            public string? PostSignatureHash { get; set; }

            public string? OrderId { get; set; }

            public string? OrderUrl { get; set; }

            public string? OrderProofContext { get; set; }

            public int? ProcessingMinutes { get; set; }

            public int? Confirmations { get; set; }

            public bool IsOfficial { get; set; }

            public bool IsStruckThrough { get; set; }

            public DateTime? TestedAt { get; set; }

            public string? ImageUrl { get; set; }

            public string? SendingTxUrl { get; set; }

            public string? ReceivingTxUrl { get; set; }

            public string? AmlScreenshotUrl { get; set; }

            public AiOrderCheckResult AiOrderCheckResult { get; set; }

            public string? AiOrderCheckSummary { get; set; }

            public DateTime CreateDate { get; set; }

            public DateTime? UpdateDate { get; set; }

            public string CreatedByUserId { get; set; } = string.Empty;

            public List<int> TagIds { get; set; } = new ();

            public List<ReplyCopy> Replies { get; set; } = new ();

            /// <summary>
            /// True when the author deleted it: their text and proof were NOT kept (only the facts
            /// that a review existed, plus other people's replies), so it cannot be restored.
            /// </summary>
            public bool ContentRemovedByAuthor { get; set; }
        }

        /// <summary>Adds the record for a review (with its tags and replies) to the context. Does not save.</summary>
        public static async Task AddForReviewAsync(IApplicationDbContext db, DirectoryEntryReview review, string deletedBy, CancellationToken ct)
        {
            var id = review.DirectoryEntryReviewId;
            var replies = await db.DirectoryEntryReviewComments.AsNoTracking()
                .Where(c => c.DirectoryEntryReviewId == id)
                .OrderBy(c => c.DirectoryEntryReviewCommentId)
                .ToListAsync(ct);
            var tagIds = await db.DirectoryEntryReviewTags.AsNoTracking()
                .Where(t => t.DirectoryEntryReviewId == id)
                .Select(t => t.ReviewTagId)
                .ToListAsync(ct);
            var listing = await db.DirectoryEntries.AsNoTracking()
                .Where(e => e.DirectoryEntryId == review.DirectoryEntryId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(ct);

            var copy = new ReviewCopy
            {
                DirectoryEntryId = review.DirectoryEntryId,
                AuthorFingerprint = review.AuthorFingerprint,
                AuthorPublicKeyArmor = review.AuthorPublicKeyArmor,
                AuthorUserId = review.AuthorUserId,
                AuthorUsername = review.AuthorUsername,
                AuthorHandle = review.AuthorHandle,
                DisplayName = review.DisplayName,
                DisplayNameSignatureArmor = review.DisplayNameSignatureArmor,
                Rating = review.Rating,
                Title = review.Title,
                Body = review.Body,
                RejectionReason = review.RejectionReason,
                ModerationStatus = review.ModerationStatus,
                PostSignatureHash = review.PostSignatureHash,
                OrderId = review.OrderId,
                OrderUrl = review.OrderUrl,
                OrderProofContext = review.OrderProofContext,
                ProcessingMinutes = review.ProcessingMinutes,
                Confirmations = review.Confirmations,
                IsOfficial = review.IsOfficial,
                IsStruckThrough = review.IsStruckThrough,
                TestedAt = review.TestedAt,
                ImageUrl = review.ImageUrl,
                SendingTxUrl = review.SendingTxUrl,
                ReceivingTxUrl = review.ReceivingTxUrl,
                AmlScreenshotUrl = review.AmlScreenshotUrl,
                AiOrderCheckResult = review.AiOrderCheckResult,
                AiOrderCheckSummary = review.AiOrderCheckSummary,
                CreateDate = review.CreateDate,
                UpdateDate = review.UpdateDate,
                CreatedByUserId = review.CreatedByUserId,
                TagIds = tagIds,
                Replies = replies.Select(Copy).ToList(),
            };

            // The author deleted it: honor that. Their text, title, order proof and screenshots are
            // not kept anywhere. What stays is the fact that a review existed (listing, author,
            // rating, dates, status, tags) and the replies OTHER people wrote under it.
            var byAuthor = deletedBy == DeletedReviewRecord.ByAuthor;
            if (byAuthor)
            {
                copy = new ReviewCopy
                {
                    DirectoryEntryId = review.DirectoryEntryId,
                    AuthorFingerprint = review.AuthorFingerprint,
                    AuthorUserId = review.AuthorUserId,
                    AuthorUsername = review.AuthorUsername,
                    Rating = review.Rating,
                    ModerationStatus = review.ModerationStatus,
                    AiOrderCheckResult = review.AiOrderCheckResult,
                    CreateDate = review.CreateDate,
                    UpdateDate = review.UpdateDate,
                    TagIds = tagIds,
                    Replies = replies
                        .Where(c => c.AuthorFingerprint != review.AuthorFingerprint || c.AuthorUserId != review.AuthorUserId)
                        .Select(Copy)
                        .ToList(),
                    ContentRemovedByAuthor = true,
                };
            }

            db.DeletedReviewRecords.Add(new DeletedReviewRecord
            {
                Kind = DeletedReviewRecord.KindReview,
                OriginalId = id,
                DirectoryEntryId = review.DirectoryEntryId,
                ListingName = listing,
                DeletedBy = deletedBy,
                DeletedUtc = DateTime.UtcNow,
                PostedUtc = review.CreateDate,
                AuthorUserId = review.AuthorUserId,
                AuthorUsername = review.AuthorUsername,
                AuthorFingerprint = review.AuthorFingerprint,
                Rating = review.Rating,
                StatusAtDelete = review.ModerationStatus.ToString(),
                Body = byAuthor ? string.Empty : review.Body,
                ReplyCount = replies.Count,
                SnapshotJson = JsonSerializer.Serialize(copy),
            });
        }

        /// <summary>Adds the record for a single reply to the context. Does not save.</summary>
        public static async Task AddForReplyAsync(IApplicationDbContext db, DirectoryEntryReviewComment reply, string deletedBy, CancellationToken ct)
        {
            var parent = await db.DirectoryEntryReviews.AsNoTracking()
                .Where(r => r.DirectoryEntryReviewId == reply.DirectoryEntryReviewId)
                .Select(r => new { r.DirectoryEntryId, Listing = r.DirectoryEntry.Name })
                .FirstOrDefaultAsync(ct);

            db.DeletedReviewRecords.Add(new DeletedReviewRecord
            {
                Kind = DeletedReviewRecord.KindReply,
                OriginalId = reply.DirectoryEntryReviewCommentId,
                ParentReviewId = reply.DirectoryEntryReviewId,
                DirectoryEntryId = parent?.DirectoryEntryId ?? 0,
                ListingName = parent?.Listing,
                DeletedBy = deletedBy,
                DeletedUtc = DateTime.UtcNow,
                PostedUtc = reply.CreateDate,
                AuthorUserId = reply.AuthorUserId,
                AuthorUsername = reply.AuthorUsername,
                AuthorFingerprint = reply.AuthorFingerprint,
                StatusAtDelete = reply.ModerationStatus.ToString(),

                // An author's own deleted reply keeps no text, only the fact that it existed.
                Body = deletedBy == DeletedReviewRecord.ByAuthor ? string.Empty : reply.Body,
                SnapshotJson = deletedBy == DeletedReviewRecord.ByAuthor ? null : JsonSerializer.Serialize(Copy(reply)),
            });
        }

        public static ReviewCopy? ReadReview(DeletedReviewRecord record)
        {
            if (record.Kind != DeletedReviewRecord.KindReview || string.IsNullOrWhiteSpace(record.SnapshotJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ReviewCopy>(record.SnapshotJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Puts a deleted review back as a NEW review held as Pending (so a moderator looks at it
        /// before it is public again), with its tags and replies. Returns the new id, or null when
        /// the record cannot be restored (a reply, no copy, already restored, listing gone).
        /// </summary>
        public static async Task<int?> RestoreReviewAsync(IApplicationDbContext db, int recordId, CancellationToken ct)
        {
            var record = await db.DeletedReviewRecords.FirstOrDefaultAsync(r => r.Id == recordId, ct);
            if (record == null || record.RestoredUtc != null)
            {
                return null;
            }

            var copy = ReadReview(record);
            if (copy == null || copy.ContentRemovedByAuthor || !await db.DirectoryEntries.AnyAsync(e => e.DirectoryEntryId == copy.DirectoryEntryId, ct))
            {
                return null;
            }

            // The author's account may have been deleted since; then the review comes back unlinked.
            var authorId = copy.AuthorUserId != null && await db.ApplicationUser.AnyAsync(u => u.Id == copy.AuthorUserId, ct)
                ? copy.AuthorUserId
                : null;

            var review = new DirectoryEntryReview
            {
                DirectoryEntryId = copy.DirectoryEntryId,
                AuthorFingerprint = copy.AuthorFingerprint,
                AuthorPublicKeyArmor = copy.AuthorPublicKeyArmor,
                AuthorUserId = authorId,
                AuthorUsername = copy.AuthorUsername,
                AuthorHandle = copy.AuthorHandle,
                DisplayName = copy.DisplayName,
                DisplayNameSignatureArmor = copy.DisplayNameSignatureArmor,
                Rating = copy.Rating,
                Title = copy.Title,
                Body = copy.Body,
                RejectionReason = copy.RejectionReason,
                ModerationStatus = ReviewModerationStatus.Pending,
                PostSignatureHash = copy.PostSignatureHash,
                OrderId = copy.OrderId,
                OrderUrl = copy.OrderUrl,
                OrderProofContext = copy.OrderProofContext,
                ProcessingMinutes = copy.ProcessingMinutes,
                Confirmations = copy.Confirmations,
                IsOfficial = copy.IsOfficial,
                IsStruckThrough = copy.IsStruckThrough,
                TestedAt = copy.TestedAt,
                ImageUrl = copy.ImageUrl,
                SendingTxUrl = copy.SendingTxUrl,
                ReceivingTxUrl = copy.ReceivingTxUrl,
                AmlScreenshotUrl = copy.AmlScreenshotUrl,
                AiOrderCheckResult = copy.AiOrderCheckResult,
                AiOrderCheckSummary = copy.AiOrderCheckSummary,
                CreateDate = copy.CreateDate,
                UpdateDate = copy.UpdateDate,
                CreatedByUserId = copy.CreatedByUserId,
            };
            db.DirectoryEntryReviews.Add(review);
            await db.SaveChangesAsync(ct);

            var liveTagIds = await db.ReviewTags.AsNoTracking().Select(t => t.ReviewTagId).ToListAsync(ct);
            foreach (var tagId in copy.TagIds.Distinct().Where(liveTagIds.Contains))
            {
                db.DirectoryEntryReviewTags.Add(new DirectoryEntryReviewTag { DirectoryEntryReviewId = review.DirectoryEntryReviewId, ReviewTagId = tagId });
            }

            foreach (var r in copy.Replies)
            {
                var replyAuthor = r.AuthorUserId != null && await db.ApplicationUser.AnyAsync(u => u.Id == r.AuthorUserId, ct) ? r.AuthorUserId : null;
                db.DirectoryEntryReviewComments.Add(new DirectoryEntryReviewComment
                {
                    DirectoryEntryReviewId = review.DirectoryEntryReviewId,
                    Body = r.Body,
                    ModerationStatus = r.ModerationStatus,
                    AuthorFingerprint = r.AuthorFingerprint,
                    AuthorUserId = replyAuthor,
                    AuthorUsername = r.AuthorUsername,
                    DisplayName = r.DisplayName,
                    CreateDate = r.CreateDate,
                    UpdateDate = r.UpdateDate,
                    CreatedByUserId = r.CreatedByUserId,
                });
            }

            record.RestoredUtc = DateTime.UtcNow;
            record.RestoredAsId = review.DirectoryEntryReviewId;
            await db.SaveChangesAsync(ct);
            return review.DirectoryEntryReviewId;
        }

        private static ReplyCopy Copy(DirectoryEntryReviewComment c) => new ()
        {
            Id = c.DirectoryEntryReviewCommentId,
            ParentCommentId = c.ParentCommentId,
            Body = c.Body,
            ModerationStatus = c.ModerationStatus,
            AuthorFingerprint = c.AuthorFingerprint,
            AuthorUserId = c.AuthorUserId,
            AuthorUsername = c.AuthorUsername,
            DisplayName = c.DisplayName,
            CreateDate = c.CreateDate,
            UpdateDate = c.UpdateDate,
            CreatedByUserId = c.CreatedByUserId,
        };
    }
}
