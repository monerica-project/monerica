using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// The admin "Users" page: member accounts and the PGP keys that posted without an account,
    /// with their posts, and the two posting blocks (account / key). Aggregates (counts, first /
    /// last post) run in the database (GROUP BY / correlated sub-queries) and lists are paged in
    /// SQL; only the current page's rows get their per-status counts and badges.
    /// No IP address is read or stored here.
    /// </summary>
    public sealed class UsersAdminService
    {
        public const int PageSize = 50;
        public const int PostsPageSize = 50;
        public const int MaxReasonLength = 200;

        private const string OwnerIndexCacheKey = "admin-users-owner-key-index";
        private static readonly TimeSpan OwnerIndexCacheFor = TimeSpan.FromMinutes(10);

        private readonly ApplicationDbContext db;
        private readonly ICacheService cacheService;
        private readonly IMemoryCache cache;

        public UsersAdminService(ApplicationDbContext db, ICacheService cacheService, IMemoryCache cache)
        {
            this.db = db;
            this.cacheService = cacheService;
            this.cache = cache;
        }

        public enum KeyBlockResult
        {
            Blocked,
            AlreadyBlocked,
            InvalidFingerprint,
            ModeratorKey,
        }

        /// <summary>A full fingerprint (40 or 64 hex digits once normalized), or null.</summary>
        public static string? NormalizeFullFingerprint(string? value)
        {
            var fp = PgpFingerprintTools.Normalize(value);
            return fp.Length is 40 or 64 ? fp : null;
        }

        // ----------------------------------------------------------------------------------------
        // Queries (public for the Postgres translation test)
        // ----------------------------------------------------------------------------------------

        /// <summary>Reviews written through the no-account PGP flow (never member posts).</summary>
        public static IQueryable<DirectoryEntryReview> PgpReviews(ApplicationDbContext db) =>
            db.DirectoryEntryReviews.AsNoTracking()
                .Where(r => r.AuthorUserId == null && !r.AuthorFingerprint.StartsWith(MemberAuthor.KeyPrefix));

        /// <summary>Replies written through the no-account PGP flows (never member posts).</summary>
        public static IQueryable<DirectoryEntryReviewComment> PgpReplies(ApplicationDbContext db) =>
            db.DirectoryEntryReviewComments.AsNoTracking()
                .Where(c => c.AuthorUserId == null && !c.AuthorFingerprint.StartsWith(MemberAuthor.KeyPrefix));

        /// <summary>One row per distinct PGP fingerprint with first / last post and post count (GROUP BY).</summary>
        public static IQueryable<KeyAggregate> KeyAggregates(ApplicationDbContext db, string? keySearch, string match)
        {
            var posts = PgpReviews(db).Select(r => new KeyPost { Fingerprint = r.AuthorFingerprint, CreateDate = r.CreateDate })
                .Concat(PgpReplies(db).Select(c => new KeyPost { Fingerprint = c.AuthorFingerprint, CreateDate = c.CreateDate }));

            var hex = PgpFingerprintTools.Normalize(keySearch);
            if (hex.Length > 0)
            {
                posts = match switch
                {
                    UsersAdminMatch.Prefix => posts.Where(p => p.Fingerprint.StartsWith(hex)),
                    UsersAdminMatch.Suffix => posts.Where(p => p.Fingerprint.EndsWith(hex)),
                    _ => posts.Where(p => p.Fingerprint.Contains(hex)),
                };
            }

            return posts
                .GroupBy(p => p.Fingerprint)
                .Select(g => new KeyAggregate
                {
                    Fingerprint = g.Key,
                    FirstSeenUtc = g.Min(p => p.CreateDate),
                    LastSeenUtc = g.Max(p => p.CreateDate),
                    PostCount = g.Count(),
                });
        }

        /// <summary>All reviews and replies of one author (member id, or PGP fingerprint), newest first.</summary>
        public static IQueryable<UserPostRow> PostsQuery(ApplicationDbContext db, string? userId, string? fingerprint)
        {
            IQueryable<DirectoryEntryReview> reviews;
            IQueryable<DirectoryEntryReviewComment> replies;
            if (userId != null)
            {
                reviews = db.DirectoryEntryReviews.AsNoTracking().Where(r => r.AuthorUserId == userId);
                replies = db.DirectoryEntryReviewComments.AsNoTracking().Where(c => c.AuthorUserId == userId);
            }
            else
            {
                var fp = fingerprint ?? string.Empty;
                reviews = PgpReviews(db).Where(r => r.AuthorFingerprint == fp);
                replies = PgpReplies(db).Where(c => c.AuthorFingerprint == fp);
            }

            return reviews.Select(r => new UserPostRow
            {
                IsReply = false,
                Id = r.DirectoryEntryReviewId,
                ReviewId = r.DirectoryEntryReviewId,
                DirectoryEntryId = r.DirectoryEntryId,
                EntryName = r.DirectoryEntry.Name,
                EntryKey = r.DirectoryEntry.DirectoryEntryKey,
                CreateDate = r.CreateDate,
                Rating = r.Rating,
                Status = r.ModerationStatus,
                Body = r.Body,
                HasOrderProof = r.OrderUrl != null || r.OrderId != null,
            })
            .Concat(replies.Select(c => new UserPostRow
            {
                IsReply = true,
                Id = c.DirectoryEntryReviewCommentId,
                ReviewId = c.DirectoryEntryReviewId,
                DirectoryEntryId = c.DirectoryEntryReview!.DirectoryEntryId,
                EntryName = c.DirectoryEntryReview.DirectoryEntry.Name,
                EntryKey = c.DirectoryEntryReview.DirectoryEntry.DirectoryEntryKey,
                CreateDate = c.CreateDate,
                Rating = null,
                Status = c.ModerationStatus,
                Body = c.Body,
                HasOrderProof = false,
            }))
            .OrderByDescending(p => p.CreateDate)
            .ThenByDescending(p => p.Id);
        }

        /// <summary>Member accounts (never admin accounts), filtered, with sort keys computed in SQL.</summary>
        public static IQueryable<AccountAggregate> AccountAggregates(ApplicationDbContext db, string? username, string? keySearch, string match, string status)
        {
            var adminIds = from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where r.NormalizedName == AuthRoles.AdminNormalized
                           select ur.UserId;

            var users = db.Users.AsNoTracking().Where(u => !adminIds.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(username))
            {
                var nq = username.Trim().ToUpperInvariant();
                users = users.Where(u => u.NormalizedUserName != null && u.NormalizedUserName.Contains(nq));
            }

            var hex = PgpFingerprintTools.Normalize(keySearch);
            if (hex.Length > 0)
            {
                users = match switch
                {
                    UsersAdminMatch.Prefix => users.Where(u => u.PgpFingerprint != null && u.PgpFingerprint.StartsWith(hex)),
                    UsersAdminMatch.Suffix => users.Where(u => u.PgpFingerprint != null && u.PgpFingerprint.EndsWith(hex)),
                    _ => users.Where(u => u.PgpFingerprint != null && u.PgpFingerprint.Contains(hex)),
                };
            }

            users = status switch
            {
                UsersAdminStatuses.Active => users.Where(u => u.DeletedUtc == null && u.DisabledUtc == null && u.PostingBlockedUtc == null),
                UsersAdminStatuses.Blocked => users.Where(u => u.DeletedUtc == null && u.PostingBlockedUtc != null),
                UsersAdminStatuses.Disabled => users.Where(u => u.DeletedUtc == null && u.DisabledUtc != null),
                UsersAdminStatuses.Deleted => users.Where(u => u.DeletedUtc != null),
                UsersAdminStatuses.All => users,
                _ => users.Where(u => u.DeletedUtc == null),
            };

            return users.Select(u => new AccountAggregate
            {
                Id = u.Id,
                Username = u.UserName,
                CreateDate = u.CreateDate,
                DeletedUtc = u.DeletedUtc,
                DisabledUtc = u.DisabledUtc,
                PostingBlockedUtc = u.PostingBlockedUtc,
                LockoutEnd = u.LockoutEnd,
                PgpFingerprint = u.PgpFingerprint,
                PostCount = db.DirectoryEntryReviews.Count(r => r.AuthorUserId == u.Id)
                    + db.DirectoryEntryReviewComments.Count(c => c.AuthorUserId == u.Id),
                LastReviewUtc = db.DirectoryEntryReviews.Where(r => r.AuthorUserId == u.Id).Max(r => (DateTime?)r.CreateDate),
                LastReplyUtc = db.DirectoryEntryReviewComments.Where(c => c.AuthorUserId == u.Id).Max(c => (DateTime?)c.CreateDate),
            });
        }

        public static IQueryable<AccountAggregate> SortAccounts(IQueryable<AccountAggregate> rows, string sort) => sort switch
        {
            UsersAdminSorts.MostPosts => rows.OrderByDescending(a => a.PostCount).ThenByDescending(a => a.CreateDate).ThenBy(a => a.Id),

            // Accounts that never posted go last (NULLs sort differently per database, so order by "has a post" first).
            UsersAdminSorts.LastActive => rows
                .OrderBy(a => a.LastReviewUtc == null && a.LastReplyUtc == null)
                .ThenByDescending(a => a.LastReviewUtc == null ? a.LastReplyUtc
                    : a.LastReplyUtc == null ? a.LastReviewUtc
                    : a.LastReviewUtc > a.LastReplyUtc ? a.LastReviewUtc : a.LastReplyUtc)
                .ThenByDescending(a => a.CreateDate)
                .ThenBy(a => a.Id),
            _ => rows.OrderByDescending(a => a.CreateDate).ThenBy(a => a.Id),
        };

        public static IQueryable<KeyAggregate> SortKeys(IQueryable<KeyAggregate> rows, string sort) => sort switch
        {
            UsersAdminSorts.MostPosts => rows.OrderByDescending(k => k.PostCount).ThenByDescending(k => k.LastSeenUtc).ThenBy(k => k.Fingerprint),
            UsersAdminSorts.LastActive => rows.OrderByDescending(k => k.LastSeenUtc).ThenBy(k => k.Fingerprint),
            _ => rows.OrderByDescending(k => k.FirstSeenUtc).ThenBy(k => k.Fingerprint),
        };

        // ----------------------------------------------------------------------------------------
        // Lists
        // ----------------------------------------------------------------------------------------
        public async Task<UsersAdminViewModel> ListAsync(UsersAdminQuery query, CancellationToken ct = default)
        {
            query.Tab = query.Tab == UsersAdminTabs.Keys ? UsersAdminTabs.Keys : UsersAdminTabs.Accounts;
            query.Sort = UsersAdminSorts.Normalize(query.Sort);
            query.Status = UsersAdminStatuses.Normalize(query.Status);
            query.Match = UsersAdminMatch.Normalize(query.Match);
            query.Q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
            query.Key = string.IsNullOrWhiteSpace(query.Key) ? null : query.Key.Trim();

            var vm = new UsersAdminViewModel { Query = query };
            if (query.Tab == UsersAdminTabs.Keys)
            {
                await this.FillKeysAsync(vm, ct);
            }
            else
            {
                await this.FillAccountsAsync(vm, ct);
            }

            return vm;
        }

        // ----------------------------------------------------------------------------------------
        // Detail pages
        // ----------------------------------------------------------------------------------------
        public async Task<AccountDetailViewModel?> GetAccountAsync(string id, int page, CancellationToken ct = default)
        {
            var row = await AccountAggregates(this.db, null, null, UsersAdminMatch.Contains, UsersAdminStatuses.All)
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync(ct);
            if (row == null)
            {
                return null;
            }

            var user = await this.db.Users.AsNoTracking().FirstAsync(u => u.Id == id, ct);
            var accounts = await this.ToAccountRowsAsync(new List<AccountAggregate> { row }, ct);
            var hidden = await this.db.DirectoryEntryReviews.CountAsync(r => r.AuthorUserId == id && r.ModerationStatus == ReviewModerationStatus.Rejected && r.RejectionReason == ReviewerBlocks.AccountHiddenReason, ct)
                + await this.db.DirectoryEntryReviewComments.CountAsync(c => c.AuthorUserId == id && c.ModerationStatus == ReviewModerationStatus.Rejected && c.RejectionReason == ReviewerBlocks.AccountHiddenReason, ct);

            return new AccountDetailViewModel
            {
                Account = accounts[0],
                PgpLinkedUtc = user.PgpLinkedUtc,
                DisabledUtc = user.DisabledUtc,
                PostingBlockedUtc = user.PostingBlockedUtc,
                PostingBlockedBy = user.PostingBlockedBy,
                PostingBlockReason = user.PostingBlockReason,
                PostingUnblockedUtc = user.PostingUnblockedUtc,
                PostingUnblockedBy = user.PostingUnblockedBy,
                HiddenByBlockCount = hidden,
                Posts = await PagePostsAsync(PostsQuery(this.db, id, null), page, ct),
            };
        }

        /// <summary>Detail for any full fingerprint (it may never have posted: it can still be blocked).</summary>
        public async Task<KeyDetailViewModel?> GetKeyAsync(string? fingerprint, int page, CancellationToken ct = default)
        {
            var fp = PgpFingerprintTools.Normalize(fingerprint);
            if (fp.Length == 0)
            {
                return null;
            }

            var agg = await KeyAggregates(this.db, null, UsersAdminMatch.Contains)
                .Where(k => k.Fingerprint == fp)
                .FirstOrDefaultAsync(ct);
            if (agg == null && NormalizeFullFingerprint(fp) == null)
            {
                return null;
            }

            var rows = await this.ToKeyRowsAsync(new List<KeyAggregate> { agg ?? new KeyAggregate { Fingerprint = fp } }, ct);
            var block = await this.db.BlockedReviewerKeys.AsNoTracking().FirstOrDefaultAsync(b => b.Fingerprint == fp, ct);
            var hidden = await PgpReviews(this.db).CountAsync(r => r.AuthorFingerprint == fp && r.ModerationStatus == ReviewModerationStatus.Rejected && r.RejectionReason == ReviewerBlocks.KeyHiddenReason, ct)
                + await PgpReplies(this.db).CountAsync(c => c.AuthorFingerprint == fp && c.ModerationStatus == ReviewModerationStatus.Rejected && c.RejectionReason == ReviewerBlocks.KeyHiddenReason, ct);

            return new KeyDetailViewModel
            {
                Key = rows[0],
                HasNoPosts = agg == null,
                Block = block,
                HiddenByBlockCount = hidden,
                Posts = await PagePostsAsync(PostsQuery(this.db, null, fp), page, ct),
            };
        }

        // ----------------------------------------------------------------------------------------
        // Key blocks
        // ----------------------------------------------------------------------------------------
        public async Task<bool> IsModeratorKeyAsync(string fingerprint)
        {
            var siteFps = await this.ModeratorFingerprintsAsync();
            return siteFps.Any(s => PgpFingerprintTools.Matches(fingerprint, s));
        }

        /// <summary>
        /// Puts a key on the block list. With <paramref name="hidePosts"/> its published or waiting
        /// PGP-flow reviews and replies are set to Rejected (reason <see cref="ReviewerBlocks.KeyHiddenReason"/>).
        /// The directory's own moderator key can never be blocked.
        /// </summary>
        public async Task<(KeyBlockResult Result, List<int> ChangedReviewIds)> BlockKeyAsync(string? fingerprint, string? reason, string? admin, bool hidePosts, CancellationToken ct = default)
        {
            var changed = new List<int>();
            var fp = NormalizeFullFingerprint(fingerprint);
            if (fp == null)
            {
                return (KeyBlockResult.InvalidFingerprint, changed);
            }

            if (await this.IsModeratorKeyAsync(fp))
            {
                return (KeyBlockResult.ModeratorKey, changed);
            }

            var now = DateTime.UtcNow;
            var row = await this.db.BlockedReviewerKeys.FirstOrDefaultAsync(b => b.Fingerprint == fp, ct);
            if (row != null && row.UnblockedUtc == null)
            {
                return (KeyBlockResult.AlreadyBlocked, changed);
            }

            if (row == null)
            {
                row = new BlockedReviewerKey { Fingerprint = fp };
                this.db.BlockedReviewerKeys.Add(row);
            }

            row.Reason = CleanReason(reason);
            row.BlockedUtc = now;
            row.BlockedBy = admin;
            row.HidPosts = hidePosts;
            row.UnblockedUtc = null;
            row.UnblockedBy = null;

            if (hidePosts)
            {
                changed = await this.HidePostsAsync(
                    this.db.DirectoryEntryReviews.Where(r => r.AuthorUserId == null && r.AuthorFingerprint == fp),
                    this.db.DirectoryEntryReviewComments.Where(c => c.AuthorUserId == null && c.AuthorFingerprint == fp),
                    ReviewerBlocks.KeyHiddenReason,
                    now,
                    ct);
            }

            await this.db.SaveChangesAsync(ct);
            return (KeyBlockResult.Blocked, changed);
        }

        /// <summary>
        /// Lifts a key block (the row stays as an audit line). With <paramref name="restorePosts"/>
        /// the posts the block hid go back to the moderation queue (Pending) — never straight to
        /// published. Returns false when the key is not blocked.
        /// </summary>
        public async Task<(bool Ok, int Restored)> UnblockKeyAsync(string? fingerprint, string? admin, bool restorePosts, CancellationToken ct = default)
        {
            var fp = PgpFingerprintTools.Normalize(fingerprint);
            var row = fp.Length == 0 ? null : await this.db.BlockedReviewerKeys.FirstOrDefaultAsync(b => b.Fingerprint == fp, ct);
            if (row == null || row.UnblockedUtc != null)
            {
                return (false, 0);
            }

            var now = DateTime.UtcNow;
            row.UnblockedUtc = now;
            row.UnblockedBy = admin;
            var restored = 0;
            if (restorePosts)
            {
                restored = await this.RestorePostsAsync(
                    this.db.DirectoryEntryReviews.Where(r => r.AuthorUserId == null && r.AuthorFingerprint == fp),
                    this.db.DirectoryEntryReviewComments.Where(c => c.AuthorUserId == null && c.AuthorFingerprint == fp),
                    ReviewerBlocks.KeyHiddenReason,
                    now,
                    ct);
            }

            await this.db.SaveChangesAsync(ct);
            return (true, restored);
        }

        // ----------------------------------------------------------------------------------------
        // Account posting blocks and linked keys
        // ----------------------------------------------------------------------------------------

        /// <summary>
        /// Blocks a member from posting (they stay signed in and can delete their own posts).
        /// With <paramref name="hidePosts"/> their published or waiting posts are set to Rejected.
        /// Never touches admin or deleted accounts.
        /// </summary>
        public async Task<(bool Ok, List<int> ChangedReviewIds)> BlockAccountAsync(string userId, string? reason, string? admin, bool hidePosts, CancellationToken ct = default)
        {
            var changed = new List<int>();
            var user = await this.FindMemberAsync(userId, ct);
            if (user == null || user.DeletedUtc != null)
            {
                return (false, changed);
            }

            var now = DateTime.UtcNow;
            if (user.PostingBlockedUtc == null)
            {
                user.PostingBlockedUtc = now;
                user.PostingBlockedBy = admin;
            }

            user.PostingBlockReason = CleanReason(reason);
            user.UpdateDate = now;

            if (hidePosts)
            {
                changed = await this.HidePostsAsync(
                    this.db.DirectoryEntryReviews.Where(r => r.AuthorUserId == userId),
                    this.db.DirectoryEntryReviewComments.Where(c => c.AuthorUserId == userId),
                    ReviewerBlocks.AccountHiddenReason,
                    now,
                    ct);
            }

            await this.db.SaveChangesAsync(ct);
            return (true, changed);
        }

        public async Task<(bool Ok, int Restored)> UnblockAccountAsync(string userId, string? admin, bool restorePosts, CancellationToken ct = default)
        {
            var user = await this.FindMemberAsync(userId, ct);
            if (user == null || user.PostingBlockedUtc == null)
            {
                return (false, 0);
            }

            var now = DateTime.UtcNow;
            user.PostingBlockedUtc = null;
            user.PostingBlockedBy = null;
            user.PostingBlockReason = null;
            user.PostingUnblockedUtc = now;
            user.PostingUnblockedBy = admin;
            user.UpdateDate = now;

            var restored = 0;
            if (restorePosts)
            {
                restored = await this.RestorePostsAsync(
                    this.db.DirectoryEntryReviews.Where(r => r.AuthorUserId == userId),
                    this.db.DirectoryEntryReviewComments.Where(c => c.AuthorUserId == userId),
                    ReviewerBlocks.AccountHiddenReason,
                    now,
                    ct);
            }

            await this.db.SaveChangesAsync(ct);
            return (true, restored);
        }

        /// <summary>Admin: unlinks the member's PGP key (frees it). Returns the removed fingerprint, or null.</summary>
        public async Task<string?> RemoveLinkedKeyAsync(string userId, CancellationToken ct = default)
        {
            var user = await this.FindMemberAsync(userId, ct);
            if (user == null || string.IsNullOrEmpty(user.PgpFingerprint))
            {
                return null;
            }

            var old = user.PgpFingerprint;
            user.PgpFingerprint = null;
            user.PgpLinkedUtc = null;
            user.UpdateDate = DateTime.UtcNow;
            await this.db.SaveChangesAsync(ct);
            MemberKeyDirectory.Invalidate(this.cache);
            return old;
        }

        private static string? CleanReason(string? reason)
        {
            var r = DirectoryManager.Utilities.Validation.UnicodeSanitizer.CleanMultiLine(reason ?? string.Empty).Trim();
            r = r.Replace('\r', ' ').Replace('\n', ' ');
            return r.Length == 0 ? null : r.Length > MaxReasonLength ? r[..MaxReasonLength] : r;
        }

        private static async Task<UserPostsPage> PagePostsAsync(IQueryable<UserPostRow> query, int page, CancellationToken ct)
        {
            var total = await query.CountAsync(ct);
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PostsPageSize));
            page = Math.Clamp(page, 1, totalPages);
            return new UserPostsPage
            {
                Total = total,
                TotalPages = totalPages,
                Page = page,
                Posts = await query.Skip((page - 1) * PostsPageSize).Take(PostsPageSize).ToListAsync(ct),
            };
        }

        private async Task FillAccountsAsync(UsersAdminViewModel vm, CancellationToken ct)
        {
            var q = vm.Query;
            var rows = AccountAggregates(this.db, q.Q, q.Key, q.Match, q.Status);
            vm.Total = await rows.CountAsync(ct);
            vm.TotalPages = Math.Max(1, (int)Math.Ceiling(vm.Total / (double)PageSize));
            vm.Page = q.Page = Math.Clamp(q.Page, 1, vm.TotalPages);

            var page = await SortAccounts(rows, q.Sort)
                .Skip((vm.Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync(ct);
            vm.Accounts = await this.ToAccountRowsAsync(page, ct);
        }

        private async Task FillKeysAsync(UsersAdminViewModel vm, CancellationToken ct)
        {
            var q = vm.Query;
            var rows = KeyAggregates(this.db, q.Key, q.Match);
            vm.Total = await rows.CountAsync(ct);
            vm.TotalPages = Math.Max(1, (int)Math.Ceiling(vm.Total / (double)PageSize));
            vm.Page = q.Page = Math.Clamp(q.Page, 1, vm.TotalPages);

            var page = await SortKeys(rows, q.Sort)
                .Skip((vm.Page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync(ct);
            vm.Keys = await this.ToKeyRowsAsync(page, ct);
            vm.ActiveKeyBlocks = await this.db.BlockedReviewerKeys.AsNoTracking()
                .Where(b => b.UnblockedUtc == null)
                .OrderByDescending(b => b.BlockedUtc)
                .ToListAsync(ct);
        }

        private async Task<List<AccountRow>> ToAccountRowsAsync(List<AccountAggregate> page, CancellationToken ct)
        {
            var ids = page.Select(a => a.Id).ToList();

            // Per-status counts for this page only (GROUP BY author, status).
            var reviewCounts = await this.db.DirectoryEntryReviews.AsNoTracking()
                .Where(r => r.AuthorUserId != null && ids.Contains(r.AuthorUserId))
                .GroupBy(r => new { r.AuthorUserId, r.ModerationStatus })
                .Select(g => new { g.Key.AuthorUserId, g.Key.ModerationStatus, Count = g.Count() })
                .ToListAsync(ct);
            var replyCounts = await this.db.DirectoryEntryReviewComments.AsNoTracking()
                .Where(c => c.AuthorUserId != null && ids.Contains(c.AuthorUserId))
                .GroupBy(c => new { c.AuthorUserId, c.ModerationStatus })
                .Select(g => new { g.Key.AuthorUserId, g.Key.ModerationStatus, Count = g.Count() })
                .ToListAsync(ct);

            var linked = page.Where(a => a.PgpFingerprint != null).Select(a => a.PgpFingerprint!).ToList();
            var blockedKeys = linked.Count == 0
                ? new HashSet<string>()
                : (await this.db.BlockedReviewerKeys.AsNoTracking()
                    .Where(b => b.UnblockedUtc == null && linked.Contains(b.Fingerprint))
                    .Select(b => b.Fingerprint)
                    .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

            var now = DateTimeOffset.UtcNow;
            return page.Select(a =>
            {
                var row = new AccountRow
                {
                    Id = a.Id,
                    Username = a.Username ?? string.Empty,
                    CreateDate = a.CreateDate,
                    IsDeleted = a.DeletedUtc != null,
                    IsDisabled = a.DisabledUtc != null,
                    IsPostingBlocked = a.PostingBlockedUtc != null,
                    IsLockedOut = a.LockoutEnd != null && a.LockoutEnd > now,
                    PgpFingerprint = a.PgpFingerprint,
                    LinkedKeyBlocked = a.PgpFingerprint != null && blockedKeys.Contains(a.PgpFingerprint),
                    LastPostUtc = Latest(a.LastReviewUtc, a.LastReplyUtc),
                };
                foreach (var c in reviewCounts.Where(c => c.AuthorUserId == a.Id))
                {
                    row.Reviews.Add(c.ModerationStatus, c.Count);
                }

                foreach (var c in replyCounts.Where(c => c.AuthorUserId == a.Id))
                {
                    row.Replies.Add(c.ModerationStatus, c.Count);
                }

                return row;
            }).ToList();
        }

        private async Task<List<KeyRow>> ToKeyRowsAsync(List<KeyAggregate> page, CancellationToken ct)
        {
            var fps = page.Select(k => k.Fingerprint).ToList();

            var reviewCounts = await PgpReviews(this.db)
                .Where(r => fps.Contains(r.AuthorFingerprint))
                .GroupBy(r => new { r.AuthorFingerprint, r.ModerationStatus })
                .Select(g => new { g.Key.AuthorFingerprint, g.Key.ModerationStatus, Count = g.Count() })
                .ToListAsync(ct);
            var replyCounts = await PgpReplies(this.db)
                .Where(c => fps.Contains(c.AuthorFingerprint))
                .GroupBy(c => new { c.AuthorFingerprint, c.ModerationStatus })
                .Select(g => new { g.Key.AuthorFingerprint, g.Key.ModerationStatus, Count = g.Count() })
                .ToListAsync(ct);

            // Distinct (key, listing) pairs, de-duplicated in SQL.
            var listingPairs = await PgpReviews(this.db)
                .Where(r => fps.Contains(r.AuthorFingerprint))
                .Select(r => new KeyListing { Fingerprint = r.AuthorFingerprint, DirectoryEntryId = r.DirectoryEntryId })
                .Concat(PgpReplies(this.db)
                    .Where(c => fps.Contains(c.AuthorFingerprint))
                    .Select(c => new KeyListing { Fingerprint = c.AuthorFingerprint, DirectoryEntryId = c.DirectoryEntryReview!.DirectoryEntryId }))
                .Distinct()
                .ToListAsync(ct);

            var members = await this.db.Users.AsNoTracking()
                .Where(u => u.PgpFingerprint != null && fps.Contains(u.PgpFingerprint))
                .Select(u => new { u.Id, u.UserName, u.PgpFingerprint, u.PostingBlockedUtc })
                .ToListAsync(ct);

            var blocked = (await this.db.BlockedReviewerKeys.AsNoTracking()
                .Where(b => b.UnblockedUtc == null && fps.Contains(b.Fingerprint))
                .Select(b => b.Fingerprint)
                .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

            var moderatorFps = await this.ModeratorFingerprintsAsync();
            var owners = await this.OwnerIndexAsync(ct);

            return page.Select(k =>
            {
                var row = new KeyRow
                {
                    Fingerprint = k.Fingerprint,
                    FirstSeenUtc = k.PostCount > 0 ? k.FirstSeenUtc : null,
                    LastSeenUtc = k.PostCount > 0 ? k.LastSeenUtc : null,
                    ListingCount = listingPairs.Count(p => p.Fingerprint == k.Fingerprint),
                    IsModerator = moderatorFps.Any(s => PgpFingerprintTools.Matches(k.Fingerprint, s)),
                    IsBlocked = blocked.Contains(k.Fingerprint),
                    OwnerOf = owners
                        .Where(o => o.Fingerprints.Any(f => PgpFingerprintTools.Matches(k.Fingerprint, f)))
                        .Select(o => o.Listing)
                        .ToList(),
                };

                var m = members.FirstOrDefault(u => u.PgpFingerprint == k.Fingerprint);
                if (m != null)
                {
                    row.LinkedMember = new LinkedMember { Id = m.Id, Username = m.UserName ?? string.Empty, IsPostingBlocked = m.PostingBlockedUtc != null };
                }

                foreach (var c in reviewCounts.Where(c => c.AuthorFingerprint == k.Fingerprint))
                {
                    row.Reviews.Add(c.ModerationStatus, c.Count);
                }

                foreach (var c in replyCounts.Where(c => c.AuthorFingerprint == k.Fingerprint))
                {
                    row.Replies.Add(c.ModerationStatus, c.Count);
                }

                return row;
            }).ToList();
        }

        private static DateTime? Latest(DateTime? a, DateTime? b) =>
            a == null ? b : b == null ? a : a > b ? a : b;

        private async Task<HashSet<string>> ModeratorFingerprintsAsync()
        {
            try
            {
                return PgpFingerprintTools.GetAllFingerprints(await this.cacheService.GetSnippetAsync(SiteConfigSetting.PgpKey));
            }
            catch (Exception)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }
        }

        /// <summary>Listings with an owner PGP key and all of that key's fingerprints (cached; keys are parsed once).</summary>
        private async Task<List<OwnerKeys>> OwnerIndexAsync(CancellationToken ct)
        {
            if (this.cache.TryGetValue(OwnerIndexCacheKey, out List<OwnerKeys>? cached) && cached != null)
            {
                return cached;
            }

            var entries = await this.db.DirectoryEntries.AsNoTracking()
                .Where(e => e.PgpKey != null && e.PgpKey != string.Empty)
                .Select(e => new { e.DirectoryEntryId, e.Name, e.DirectoryEntryKey, e.PgpKey })
                .ToListAsync(ct);

            var index = entries
                .Select(e => new OwnerKeys
                {
                    Listing = new OwnedListing { DirectoryEntryId = e.DirectoryEntryId, Name = e.Name ?? string.Empty, Key = e.DirectoryEntryKey },
                    Fingerprints = PgpFingerprintTools.GetAllFingerprints(e.PgpKey),
                })
                .Where(o => o.Fingerprints.Count > 0)
                .ToList();
            this.cache.Set(OwnerIndexCacheKey, index, OwnerIndexCacheFor);
            return index;
        }

        private async Task<ApplicationUser?> FindMemberAsync(string userId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var isAdmin = await (from ur in this.db.UserRoles
                                 join r in this.db.Roles on ur.RoleId equals r.Id
                                 where ur.UserId == userId && r.NormalizedName == AuthRoles.AdminNormalized
                                 select ur.UserId).AnyAsync(ct);
            return isAdmin ? null : await this.db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        }

        private async Task<List<int>> HidePostsAsync(
            IQueryable<DirectoryEntryReview> reviewsQuery,
            IQueryable<DirectoryEntryReviewComment> repliesQuery,
            string reason,
            DateTime now,
            CancellationToken ct)
        {
            var changed = new List<int>();
            var reviews = await reviewsQuery
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
                            || r.ModerationStatus == ReviewModerationStatus.Pending
                            || r.ModerationStatus == ReviewModerationStatus.Flagged)
                .ToListAsync(ct);
            foreach (var r in reviews)
            {
                if (r.ModerationStatus == ReviewModerationStatus.Approved)
                {
                    changed.Add(r.DirectoryEntryReviewId);
                }

                r.ModerationStatus = ReviewModerationStatus.Rejected;
                r.RejectionReason = reason;
                r.UpdateDate = now;
            }

            var replies = await repliesQuery
                .Where(c => c.ModerationStatus == ReviewModerationStatus.Approved
                            || c.ModerationStatus == ReviewModerationStatus.Pending
                            || c.ModerationStatus == ReviewModerationStatus.Flagged)
                .ToListAsync(ct);
            foreach (var c in replies)
            {
                if (c.ModerationStatus == ReviewModerationStatus.Approved)
                {
                    changed.Add(c.DirectoryEntryReviewId);
                }

                c.ModerationStatus = ReviewModerationStatus.Rejected;
                c.RejectionReason = reason;
                c.UpdateDate = now;
            }

            return changed.Distinct().ToList();
        }

        // Only posts the block itself hid (exact marker reason) go back, and only to Pending.
        private async Task<int> RestorePostsAsync(
            IQueryable<DirectoryEntryReview> reviewsQuery,
            IQueryable<DirectoryEntryReviewComment> repliesQuery,
            string reason,
            DateTime now,
            CancellationToken ct)
        {
            var reviews = await reviewsQuery
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Rejected && r.RejectionReason == reason)
                .ToListAsync(ct);
            foreach (var r in reviews)
            {
                r.ModerationStatus = ReviewModerationStatus.Pending;
                r.RejectionReason = string.Empty;
                r.UpdateDate = now;
            }

            var replies = await repliesQuery
                .Where(c => c.ModerationStatus == ReviewModerationStatus.Rejected && c.RejectionReason == reason)
                .ToListAsync(ct);
            foreach (var c in replies)
            {
                c.ModerationStatus = ReviewModerationStatus.Pending;
                c.RejectionReason = null;
                c.UpdateDate = now;
            }

            return reviews.Count + replies.Count;
        }

        public sealed class KeyPost
        {
            public string Fingerprint { get; set; } = string.Empty;

            public DateTime CreateDate { get; set; }
        }

        public sealed class KeyListing
        {
            public string Fingerprint { get; set; } = string.Empty;

            public int DirectoryEntryId { get; set; }
        }

        public sealed class KeyAggregate
        {
            public string Fingerprint { get; set; } = string.Empty;

            public DateTime FirstSeenUtc { get; set; }

            public DateTime LastSeenUtc { get; set; }

            public int PostCount { get; set; }
        }

        public sealed class AccountAggregate
        {
            public string Id { get; set; } = string.Empty;

            public string? Username { get; set; }

            public DateTime CreateDate { get; set; }

            public DateTime? DeletedUtc { get; set; }

            public DateTime? DisabledUtc { get; set; }

            public DateTime? PostingBlockedUtc { get; set; }

            public DateTimeOffset? LockoutEnd { get; set; }

            public string? PgpFingerprint { get; set; }

            public int PostCount { get; set; }

            public DateTime? LastReviewUtc { get; set; }

            public DateTime? LastReplyUtc { get; set; }
        }

        private sealed class OwnerKeys
        {
            public OwnedListing Listing { get; set; } = new ();

            public HashSet<string> Fingerprints { get; set; } = new ();
        }
    }
}
