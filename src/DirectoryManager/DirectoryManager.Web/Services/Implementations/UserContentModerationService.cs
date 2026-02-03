using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public sealed class UserContentModerationService : IUserContentModerationService
    {
        private readonly ISearchBlacklistCache blacklistCache;

        public UserContentModerationService(ISearchBlacklistCache blacklistCache)
        {
            this.blacklistCache = blacklistCache;
        }

        public async Task<UserContentModerationResult> EvaluateReviewAsync(string? body, CancellationToken ct)
        {
            return await this.EvaluateAsync(
                body,
                isReply: false,
                ct: ct);
        }

        public async Task<UserContentModerationResult> EvaluateReplyAsync(string? body, CancellationToken ct)
        {
            return await this.EvaluateAsync(
                body,
                isReply: true,
                ct: ct);
        }

        private static bool ContainsBlacklistTerm(string text, HashSet<string> terms)
        {
            if (string.IsNullOrWhiteSpace(text) || terms is null || terms.Count == 0)
            {
                return false;
            }

            var haystack = text.ToLowerInvariant();

            foreach (var term in terms)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                // terms should already be normalized to lowercase by the cache,
                // but this remains safe either way.
                if (haystack.Contains(term.Trim().ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<UserContentModerationResult> EvaluateAsync(string? body, bool isReply, CancellationToken ct)
        {
            var trimmed = (body ?? string.Empty).Trim();

            // 1) Minimum length (don’t mention exact length)
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length <= IntegerConstants.MinLengthCommentChars)
            {
                return new UserContentModerationResult
                {
                    IsValid = false,
                    NeedsManualReview = false,
                    ValidationErrorMessage = isReply
                        ? "Please add a bit more detail so your reply is helpful to others."
                        : "Please add a bit more detail so your review is helpful to others.",
                    ThankYouMessage = string.Empty
                };
            }

            // 2) Reject scripts or HTML outright (user must remove it)
            if (ScriptValidation.ContainsScriptTag(trimmed) || HtmlValidation.ContainsHtmlTag(trimmed))
            {
                return new UserContentModerationResult
                {
                    IsValid = false,
                    NeedsManualReview = false,
                    ValidationErrorMessage = "Please remove any HTML or scripts. This must be plain text.",
                    ThankYouMessage = string.Empty
                };
            }

            // 3) Manual review triggers: blacklist OR hyperlink
            var terms = await this.blacklistCache.GetTermsAsync(ct); // expected normalized/lowercase
            bool hasBlacklistTerm = ContainsBlacklistTerm(trimmed, terms);

            // hyperlink => pending (but still allowed)
            bool hasLink = Utilities.Helpers.TextHelper.ContainsHyperlink(trimmed);

            bool needsManualReview = hasBlacklistTerm || hasLink;

            var thankYou = isReply
                ? (needsManualReview
                    ? "Thanks! Your reply has been received and will be checked before it appears."
                    : "Thanks! Your reply has been received and should appear shortly.")
                : (needsManualReview
                    ? "Thanks for your review! It has been received and will be checked before it appears."
                    : "Thanks for your review! It has been received and should appear shortly.");

            return new UserContentModerationResult
            {
                IsValid = true,
                NeedsManualReview = needsManualReview,
                ValidationErrorMessage = null,
                ThankYouMessage = thankYou
            };
        }
    }
}