using DirectoryManager.Data.Models;
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
    /// A member can link a PGP key to their account by decrypting a one-time code, like the PGP
    /// review flow. The key is then shown next to their username ("PGP-verified"). It grants no
    /// listing-owner or moderator powers; see <see cref="MemberPgpKeyService"/>.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.Member)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class MemberPgpKeyController : BaseController
    {
        private readonly MemberAccountService accounts;
        private readonly MemberPgpKeyService keys;

        public MemberPgpKeyController(MemberAccountService accounts, MemberPgpKeyService keys, IMemoryCache cache)
            : base(cache)
        {
            this.accounts = accounts;
            this.keys = keys;
        }

        [HttpGet("members/pgp-key")]
        public async Task<IActionResult> Index()
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            return this.View(this.BuildPage(user, null));
        }

        [HttpPost("members/pgp-key")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPost(string? password, string? pgpArmored, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var result = await this.keys.StartAsync(user, password, pgpArmored, ct);
            if (!result.Succeeded)
            {
                this.ModelState.AddModelError(result.Field, result.Error ?? MemberPgpKeyService.InvalidKeyMessage);
                return this.View(nameof(this.Index), this.BuildPage(user, pgpArmored));
            }

            return this.LocalRedirect(MemberAuth.PgpKeyVerifyPath);
        }

        [HttpGet("members/pgp-key/verify")]
        public async Task<IActionResult> Verify()
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var pending = this.keys.GetPending(user);
            if (pending == null)
            {
                this.TempData["MemberMessage"] = MemberPgpKeyService.ExpiredMessage;
                return this.LocalRedirect(MemberAuth.PgpKeyPath);
            }

            return this.View(BuildVerify(user, pending));
        }

        [HttpPost("members/pgp-key/verify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPost(string? fingerprint, string? code, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var wasReplace = !string.IsNullOrEmpty(user.PgpFingerprint);
            var result = await this.keys.ConfirmAsync(user, fingerprint, code, ct);
            if (!result.Succeeded)
            {
                if (result.Pending != null)
                {
                    // Wrong code: same page, same encrypted message, attempts counted.
                    this.ModelState.AddModelError(string.Empty, result.Error ?? MemberPgpKeyService.WrongCodeMessage);
                    return this.View(nameof(this.Verify), BuildVerify(user, result.Pending));
                }

                this.ModelState.AddModelError(string.Empty, result.Error ?? MemberPgpKeyService.ExpiredMessage);
                return this.View(nameof(this.Index), this.BuildPage(user, null));
            }

            await this.RepublishPostsAsync(user, "member linked a PGP key", ct);
            this.TempData["MemberMessage"] = wasReplace
                ? "Your new PGP key is verified and replaced the old one on your account."
                : "Your PGP key is verified and linked to your account.";
            return this.LocalRedirect(MemberAuth.PgpKeyPath);
        }

        [HttpPost("members/pgp-key/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePost(string? password, CancellationToken ct)
        {
            var user = await this.accounts.FindValidMemberAsync(this.User);
            if (user == null)
            {
                return this.LocalRedirect(MemberAuth.SignInPath);
            }

            var result = await this.keys.RemoveAsync(user, password, ct);
            if (!result.Succeeded)
            {
                this.ModelState.AddModelError("removePassword", result.Error ?? MemberPgpKeyService.WrongPasswordMessage);
                return this.View(nameof(this.Index), this.BuildPage(user, null));
            }

            await this.RepublishPostsAsync(user, "member removed their PGP key", ct);
            this.TempData["MemberMessage"] = "The PGP key was removed from your account.";
            return this.LocalRedirect(MemberAuth.PgpKeyPath);
        }

        private static MemberPgpKeyVerifyViewModel BuildVerify(ApplicationUser user, MemberPgpKeyService.PendingKeyLink pending) => new ()
        {
            Fingerprint = pending.Fingerprint,
            Ciphertext = pending.Ciphertext,
            ExpiresUtc = pending.ExpiresUtc,
            IsReplace = !string.IsNullOrEmpty(user.PgpFingerprint),
        };

        private MemberPgpKeyViewModel BuildPage(ApplicationUser user, string? pastedKey) => new ()
        {
            Username = user.UserName ?? string.Empty,
            LinkedFingerprint = user.PgpFingerprint,
            LinkedUtc = user.PgpLinkedUtc,
            PendingFingerprint = this.keys.GetPending(user)?.Fingerprint,
            PgpArmored = pastedKey != null && pastedKey.Length <= IntegerConstants.MaxPgpArmoredChars ? pastedKey : null,
        };

        // Bylines on published pages follow the account's key: refresh the static mirror.
        private async Task RepublishPostsAsync(ApplicationUser user, string reason, CancellationToken ct)
        {
            var (reviewIds, replyIds) = await this.keys.PublishedPostsAsync(user.Id, ct);
            if (reviewIds.Count == 0 && replyIds.Count == 0)
            {
                return;
            }

            var publish = this.StaticPublish();
            foreach (var id in reviewIds)
            {
                await publish.EnqueueForReviewAsync(id, reason, ct);
            }

            foreach (var id in replyIds)
            {
                await publish.EnqueueForReplyAsync(id, reason, ct);
            }
        }
    }
}
