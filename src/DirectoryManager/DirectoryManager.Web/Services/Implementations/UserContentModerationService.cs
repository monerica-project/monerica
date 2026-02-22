using System.Text.RegularExpressions;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public sealed class UserContentModerationService : IUserContentModerationService
    {
        // Treat letters/digits as “word” characters for boundary checks (Unicode-safe)
        private const string WordCharClass = @"[\p{L}\p{Nd}]";

        private readonly ISearchBlacklistCache blacklistCache;

        public UserContentModerationService(ISearchBlacklistCache blacklistCache)
        {
            this.blacklistCache = blacklistCache;
        }

        public Task<UserContentModerationResult> EvaluateReviewAsync(string? body, CancellationToken ct)
            => this.EvaluateAsync(body, isReply: false, ct: ct);

        public Task<UserContentModerationResult> EvaluateReplyAsync(string? body, CancellationToken ct)
            => this.EvaluateAsync(body, isReply: true, ct: ct);

        /// <summary>
        /// Returns true if the content contains a blacklist term as a whole word / phrase,
        /// not as a substring inside another word.
        /// Example: "meth" will NOT match "something".
        /// </summary>
        private static bool ContainsBlacklistTermWholeWord(string text, HashSet<string> terms)
        {
            if (string.IsNullOrWhiteSpace(text) || terms is null || terms.Count == 0)
            {
                return false;
            }

            // Normalize once
            var haystack = text.Trim();

            foreach (var raw in terms)
            {
                var term = (raw ?? string.Empty).Trim();
                if (term.Length == 0)
                {
                    continue;
                }

                // Build a safe pattern:
                // - escapes special characters
                // - if phrase, allow whitespace between tokens
                // - forces “not preceded/followed by letter/digit” so no substring matches
                var pattern = BuildWholeWordOrPhrasePattern(term);

                if (Regex.IsMatch(
                        haystack,
                        pattern,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildWholeWordOrPhrasePattern(string term)
        {
            // Escape everything first
            var escaped = Regex.Escape(term);

            // If the term includes spaces/tabs/newlines, treat it as a phrase and allow flexible whitespace
            // (Regex.Escape turns spaces into "\ ", so replace any escaped whitespace runs with "\s+")
            escaped = Regex.Replace(escaped, @"(\\\s)+", @"\s+");

            // Whole-word/phrase boundaries:
            // (?<![\p{L}\p{Nd}])  not preceded by a letter/digit
            // (?![\p{L}\p{Nd}])   not followed by a letter/digit
            return $@"(?<!{WordCharClass}){escaped}(?!{WordCharClass})";
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
            bool hasBlacklistTerm = ContainsBlacklistTermWholeWord(trimmed, terms);

            // hyperlink => pending (but still allowed)
            bool hasLink = Utilities.Helpers.StringHelpers.ContainsHyperlink(trimmed);

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