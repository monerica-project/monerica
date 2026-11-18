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
    /// Admin: disable (ban: can't sign in at all) or re-enable a member account. The member list
    /// itself moved to the "Users" page (<see cref="UsersAdminController"/>); /admin/members still
    /// opens its Accounts tab.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [Route("admin/members")]
    public class MembersAdminController : BaseController
    {
        private readonly MemberAccountService accounts;
        private readonly ILogger<MembersAdminController> logger;

        public MembersAdminController(MemberAccountService accounts, ILogger<MembersAdminController> logger, IMemoryCache cache)
            : base(cache)
        {
            this.accounts = accounts;
            this.logger = logger;
        }

        [HttpGet("")]
        public IActionResult Index(string? q, int page = 1)
        {
            var url = "/admin/users?tab=" + UsersAdminTabs.Accounts;
            if (!string.IsNullOrWhiteSpace(q))
            {
                url += "&q=" + Uri.EscapeDataString(q.Trim()) + "&status=" + UsersAdminStatuses.All;
            }

            if (page > 1)
            {
                url += "&page=" + page;
            }

            return this.Redirect(url);
        }

        [HttpPost("{id}/disable")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable(string id, bool hideReviews, bool confirm, CancellationToken ct)
        {
            if (!confirm)
            {
                this.TempData["UsersError"] = UsersAdminController.ConfirmMessage;
                return this.Redirect(UsersAdminController.AccountUrl(id));
            }

            var (ok, changedReviewIds) = await this.accounts.SetDisabledAsync(id, disabled: true, hideReviews, ct);
            if (!ok)
            {
                this.TempData["UsersError"] = "That account can't be disabled here (not found, deleted, or an admin).";
                return this.Redirect(UsersAdminController.AccountUrl(id));
            }

            this.ClearCachedItems();
            foreach (var reviewId in changedReviewIds)
            {
                await this.StaticPublish().EnqueueForReviewAsync(reviewId, "member account disabled: posts hidden", ct);
            }

            this.logger.LogInformation("Admin {Admin} disabled member account {UserId} (hide posts: {HidePosts}).", this.User?.Identity?.Name, id, hideReviews);
            this.TempData["UsersMessage"] = hideReviews
                ? $"Account disabled and its posts hidden ({changedReviewIds.Count} published review page(s) updated)."
                : "Account disabled. Its posts were left as they are.";
            return this.Redirect(UsersAdminController.AccountUrl(id));
        }

        [HttpPost("{id}/enable")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable(string id, bool confirm, CancellationToken ct)
        {
            if (!confirm)
            {
                this.TempData["UsersError"] = UsersAdminController.ConfirmMessage;
                return this.Redirect(UsersAdminController.AccountUrl(id));
            }

            var (ok, _) = await this.accounts.SetDisabledAsync(id, disabled: false, hideReviews: false, ct);
            if (ok)
            {
                this.logger.LogInformation("Admin {Admin} re-enabled member account {UserId}.", this.User?.Identity?.Name, id);
            }

            this.TempData[ok ? "UsersMessage" : "UsersError"] = ok
                ? "Account enabled. Hidden posts stay hidden until you approve them again."
                : "That account can't be changed here.";
            return this.Redirect(UsersAdminController.AccountUrl(id));
        }
    }
}
