using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Admin "Users": every member account and every PGP key that posted without an account, with
    /// their posts, plus posting blocks for both. Every change is a POST with antiforgery and a
    /// confirm checkbox, and is written to the app log (admin name + account id / fingerprint; no
    /// IP addresses).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [Route("admin/users")]
    public class UsersAdminController : BaseController
    {
        public const string ConfirmMessage = "Tick the confirmation box to make this change.";

        private readonly UsersAdminService users;
        private readonly MemberPgpKeyService memberKeys;
        private readonly ILogger<UsersAdminController> logger;

        public UsersAdminController(
            UsersAdminService users,
            MemberPgpKeyService memberKeys,
            ILogger<UsersAdminController> logger,
            IMemoryCache cache)
            : base(cache)
        {
            this.users = users;
            this.memberKeys = memberKeys;
            this.logger = logger;
        }

        public static string AccountUrl(string id) => "/admin/users/account/" + Uri.EscapeDataString(id);

        public static string KeyUrl(string fingerprint) => "/admin/users/key/" + Uri.EscapeDataString(fingerprint);

        [HttpGet("")]
        public async Task<IActionResult> Index(
            string? tab,
            string? q,
            string? key,
            string? match,
            string? status,
            string? sort,
            int page = 1,
            CancellationToken ct = default)
        {
            var vm = await this.users.ListAsync(
                new UsersAdminQuery
                {
                    Tab = tab ?? UsersAdminTabs.Accounts,
                    Q = q,
                    Key = key,
                    Match = match ?? UsersAdminMatch.Contains,
                    Status = status ?? UsersAdminStatuses.Current,
                    Sort = sort ?? UsersAdminSorts.Newest,
                    Page = page,
                },
                ct);
            return this.View(vm);
        }

        [HttpGet("account/{id}")]
        public async Task<IActionResult> Account(string id, int page = 1, CancellationToken ct = default)
        {
            var vm = await this.users.GetAccountAsync(id, page, ct);
            return vm == null ? this.NotFound() : this.View(vm);
        }

        /// <summary>Opens a fingerprint typed into the lookup box (any full fingerprint, posted or not).</summary>
        [HttpGet("key")]
        public IActionResult KeyLookup(string? fingerprint)
        {
            var fp = UsersAdminService.NormalizeFullFingerprint(fingerprint);
            if (fp == null)
            {
                this.TempData["UsersError"] = "Enter a full fingerprint (40 or 64 hex characters). To find a key by part of it, use the search on the list.";
                return this.Redirect("/admin/users?tab=" + UsersAdminTabs.Keys);
            }

            return this.Redirect(KeyUrl(fp));
        }

        [HttpGet("key/{fingerprint}")]
        public async Task<IActionResult> Key(string fingerprint, int page = 1, CancellationToken ct = default)
        {
            var vm = await this.users.GetKeyAsync(fingerprint, page, ct);
            return vm == null ? this.NotFound() : this.View(vm);
        }

        [HttpPost("account/{id}/block")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockAccount(string id, string? reason, bool hidePosts, bool confirm, CancellationToken ct)
        {
            if (!confirm)
            {
                return this.Back(AccountUrl(id), ConfirmMessage, error: true);
            }

            var (ok, changed) = await this.users.BlockAccountAsync(id, reason, this.AdminName, hidePosts, ct);
            if (!ok)
            {
                return this.Back(AccountUrl(id), "That account can't be blocked here (not found, deleted, or an admin).", error: true);
            }

            await this.RepublishAsync(changed, "member blocked from posting: posts hidden", ct);
            this.logger.LogInformation(
                "Admin {Admin} blocked member account {UserId} from posting (hide posts: {HidePosts}, {Changed} published review page(s) changed).",
                this.AdminName,
                id,
                hidePosts,
                changed.Count);
            return this.Back(
                AccountUrl(id),
                hidePosts
                    ? $"Blocked from posting, and the account's posts were hidden ({changed.Count} published review page(s) updated)."
                    : "Blocked from posting. Existing posts were left as they are.");
        }

        [HttpPost("account/{id}/unblock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockAccount(string id, bool restorePosts, bool confirm, CancellationToken ct)
        {
            if (!confirm)
            {
                return this.Back(AccountUrl(id), ConfirmMessage, error: true);
            }

            var (ok, restored) = await this.users.UnblockAccountAsync(id, this.AdminName, restorePosts, ct);
            if (!ok)
            {
                return this.Back(AccountUrl(id), "That account is not blocked from posting.", error: true);
            }

            this.ClearCachedItems();
            this.logger.LogInformation(
                "Admin {Admin} lifted the posting block on member account {UserId} ({Restored} hidden post(s) back to moderation).",
                this.AdminName,
                id,
                restored);
            return this.Back(
                AccountUrl(id),
                restorePosts
                    ? $"Posting block lifted. {restored} post(s) hidden by the block are back in the moderation queue (waiting)."
                    : "Posting block lifted. Hidden posts stay hidden.");
        }

        [HttpPost("account/{id}/remove-key")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveKey(string id, bool confirm, CancellationToken ct)
        {
            if (!confirm)
            {
                return this.Back(AccountUrl(id), ConfirmMessage, error: true);
            }

            var removed = await this.users.RemoveLinkedKeyAsync(id, ct);
            if (removed == null)
            {
                return this.Back(AccountUrl(id), "That account has no linked PGP key (or can't be changed here).", error: true);
            }

            // Bylines on published posts follow the account's current key: refresh them.
            var (reviewIds, replyIds) = await this.memberKeys.PublishedPostsAsync(id, ct);
            this.ClearCachedItems();
            var publish = this.StaticPublish();
            foreach (var reviewId in reviewIds)
            {
                await publish.EnqueueForReviewAsync(reviewId, "admin removed a member's PGP key", ct);
            }

            foreach (var replyId in replyIds)
            {
                await publish.EnqueueForReplyAsync(replyId, "admin removed a member's PGP key", ct);
            }

            this.logger.LogInformation("Admin {Admin} removed PGP key {Fingerprint} from member account {UserId}.", this.AdminName, removed, id);
            return this.Back(AccountUrl(id), $"PGP key {removed} was removed from this account. It can now be linked again (by any account).");
        }

        [HttpPost("key/{fingerprint}/block")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockKey(string fingerprint, string? reason, bool hidePosts, bool confirm, CancellationToken ct)
        {
            var fp = UsersAdminService.NormalizeFullFingerprint(fingerprint) ?? fingerprint;
            if (!confirm)
            {
                return this.Back(KeyUrl(fp), ConfirmMessage, error: true);
            }

            var (result, changed) = await this.users.BlockKeyAsync(fp, reason, this.AdminName, hidePosts, ct);
            switch (result)
            {
                case UsersAdminService.KeyBlockResult.ModeratorKey:
                    this.logger.LogWarning("Admin {Admin} tried to block the directory's own moderator key {Fingerprint}; refused.", this.AdminName, fp);
                    return this.Back(KeyUrl(fp), "This is the directory's own moderator key. It can't be blocked.", error: true);
                case UsersAdminService.KeyBlockResult.InvalidFingerprint:
                    return this.Back("/admin/users?tab=" + UsersAdminTabs.Keys, "That is not a full PGP fingerprint.", error: true);
                case UsersAdminService.KeyBlockResult.AlreadyBlocked:
                    return this.Back(KeyUrl(fp), "This key is already blocked.", error: true);
            }

            await this.RepublishAsync(changed, "PGP key blocked: posts hidden", ct);
            this.logger.LogInformation(
                "Admin {Admin} blocked reviewer key {Fingerprint} (hide posts: {HidePosts}, {Changed} published review page(s) changed).",
                this.AdminName,
                fp,
                hidePosts,
                changed.Count);
            return this.Back(
                KeyUrl(fp),
                hidePosts
                    ? $"Key blocked, and its posts were hidden ({changed.Count} published review page(s) updated)."
                    : "Key blocked. Existing posts were left as they are.");
        }

        [HttpPost("key/{fingerprint}/unblock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockKey(string fingerprint, bool restorePosts, bool confirm, CancellationToken ct)
        {
            var fp = UsersAdminService.NormalizeFullFingerprint(fingerprint) ?? fingerprint;
            if (!confirm)
            {
                return this.Back(KeyUrl(fp), ConfirmMessage, error: true);
            }

            var (ok, restored) = await this.users.UnblockKeyAsync(fp, this.AdminName, restorePosts, ct);
            if (!ok)
            {
                return this.Back(KeyUrl(fp), "This key is not blocked.", error: true);
            }

            this.ClearCachedItems();
            this.logger.LogInformation(
                "Admin {Admin} unblocked reviewer key {Fingerprint} ({Restored} hidden post(s) back to moderation).",
                this.AdminName,
                fp,
                restored);
            return this.Back(
                KeyUrl(fp),
                restorePosts
                    ? $"Key unblocked. {restored} post(s) hidden by the block are back in the moderation queue (waiting)."
                    : "Key unblocked. Hidden posts stay hidden.");
        }

        private string AdminName => this.User?.Identity?.Name ?? "admin";

        private IActionResult Back(string url, string message, bool error = false)
        {
            this.TempData[error ? "UsersError" : "UsersMessage"] = message;
            return this.Redirect(url);
        }

        private async Task RepublishAsync(List<int> changedReviewIds, string reason, CancellationToken ct)
        {
            this.ClearCachedItems();
            foreach (var reviewId in changedReviewIds)
            {
                await this.StaticPublish().EnqueueForReviewAsync(reviewId, reason, ct);
            }
        }
    }
}
