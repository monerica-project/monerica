using System.Globalization;
using System.Text;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.ReviewOrderChecker.Check
{
    /// <summary>
    /// Checks the order links of reviews waiting for moderation and records what was found in the
    /// review's AiOrder* columns. It never changes a review's text, rating, moderation status or
    /// tags, and never notifies anyone: the moderator reads the result and decides.
    /// </summary>
    public sealed class OrderCheckRunner
    {
        public const string ValidOrderSlug = "valid-order";

        // Limits for numbers read off an order page. Anything above them is treated as "unknown".
        private const decimal MaxPlausibleAmount = 1_000_000_000m;
        private const decimal MaxPlausibleUnitUsd = 10_000_000m;
        private const decimal MaxPlausibleUsd = 1_000_000_000_000m;

        private readonly ApplicationDbContext db;
        private readonly IOrderPageReader reader;
        private readonly IClaudeOrderCheckService claude;
        private readonly IPriceLookup prices;
        private readonly OrderCheckOptions options;

        public OrderCheckRunner(ApplicationDbContext db, IOrderPageReader reader, IClaudeOrderCheckService claude, IPriceLookup prices, OrderCheckOptions options)
        {
            this.db = db;
            this.reader = reader;
            this.claude = claude;
            this.prices = prices;
            this.options = options;
        }

        public async Task<int> RunAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var usedToday = await this.db.AiCallLogs.CountAsync(l => l.Feature == AiCallLog.FeatureReviewOrderCheck && l.CreatedUtc >= today, ct);
            if (usedToday >= this.options.DailyCap)
            {
                Console.WriteLine($"Daily cap reached ({usedToday}/{this.options.DailyCap}).");
                return 0;
            }

            var since = now.AddDays(-this.options.MaxReviewAgeDays);
            var recheckBefore = now.AddMinutes(-this.options.RecheckAfterMinutes);
            var max = this.options.MaxChecksPerReview;
            var due = await this.db.DirectoryEntryReviews
                .Include(r => r.DirectoryEntry)
                .Include(r => r.ReviewTags).ThenInclude(t => t.ReviewTag)
                .Where(r => r.OrderUrl != null && r.OrderUrl != string.Empty)
                .Where(r => r.AiOrderCheckQueued
                    || (r.ModerationStatus == ReviewModerationStatus.Pending && !r.IsOfficial && r.CreateDate >= since
                        && (r.AiOrderCheckResult == AiOrderCheckResult.NotChecked
                            || (r.AiOrderCheckResult == AiOrderCheckResult.InProgress
                                && r.AiOrderCheckAttempts < max
                                && r.AiOrderCheckedUtc < recheckBefore))))
                .OrderByDescending(r => r.AiOrderCheckQueued)
                .ThenBy(r => r.DirectoryEntryReviewId)
                .Take(this.options.PerRunCap)
                .ToListAsync(ct);
            if (due.Count == 0)
            {
                return 0;
            }

            var tags = await this.db.ReviewTags.AsNoTracking().Where(t => t.IsEnabled).ToListAsync(ct);
            int done = 0;
            foreach (var review in due)
            {
                if (usedToday >= this.options.DailyCap)
                {
                    break;
                }

                // One bad review must never stop the run or be retried forever: any failure is
                // recorded on that review (so its attempt count rises and the AI calls already
                // made are saved and counted against the daily cap), then the run moves on.
                try
                {
                    usedToday += await this.CheckOneAsync(review, tags, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Out of time: keep what was logged so far, count the attempt, stop the run.
                    review.AiOrderCheckQueued = false;
                    review.AiOrderCheckAttempts++;
                    review.AiOrderCheckedUtc = DateTime.UtcNow;
                    await this.db.SaveChangesAsync(CancellationToken.None);
                    throw;
                }
                catch (Exception ex)
                {
                    usedToday++; // at least one call may have been made; the saved log is the real count
                    review.AiOrderCheckResult = AiOrderCheckResult.CouldNotVerify;
                    review.AiOrderCheckSummary = Truncate("The check stopped with an error (" + ex.GetType().Name + ": " + ex.Message + "). Use \"Check again\" or check by hand.", 1500);
                    Console.WriteLine($"Review #{review.DirectoryEntryReviewId}: check failed: {ex}");
                }

                review.AiOrderCheckQueued = false;
                review.AiOrderCheckAttempts++;
                review.AiOrderCheckedUtc = DateTime.UtcNow;
                await this.db.SaveChangesAsync(ct);
                Console.WriteLine($"Review #{review.DirectoryEntryReviewId}: {review.AiOrderCheckResult}. {review.AiOrderCheckSummary}");
                done++;
            }

            return done;
        }

        /// <summary>Checks one review and fills its AiOrder* columns. Returns the number of AI calls made.</summary>
        public async Task<int> CheckOneAsync(DirectoryEntryReview review, IReadOnlyList<ReviewTag> tags, CancellationToken ct)
        {
            var log = new StringBuilder();
            void Step(string line) => log.AppendLine($"{DateTime.UtcNow:HH:mm:ss} {line}");
            void Store(AiOrderCheckResult result, string summary, OrderProofStatus? status = null, decimal? usd = null, string? suggested = null, string? model = null)
            {
                review.AiOrderCheckResult = result;
                review.AiOrderCheckSummary = Truncate(summary, 1500);
                review.AiOrderStatus = status;
                review.AiOrderUsdValue = usd;
                review.AiSuggestedTags = suggested is { Length: > 0 } ? Truncate(suggested, 400) : null;
                review.AiOrderCheckModel = model;
                review.AiOrderCheckDetails = log.ToString();
            }

            // Order pages expire. A later check that can no longer read the page must not wipe out an
            // earlier check that saw the finished order; the earlier finding is kept and noted.
            var earlier = review.AiOrderCheckResult == AiOrderCheckResult.ChecksOut && review.AiOrderCheckedUtc is { } when
                ? new { When = when, review.AiOrderCheckSummary, review.AiOrderStatus, review.AiOrderUsdValue, review.AiSuggestedTags, review.AiOrderCheckModel, review.AiOrderCheckDetails }
                : null;
            if (earlier != null)
            {
                Step($"Earlier check on {earlier.When:yyyy-MM-dd HH:mm} UTC found the order checks out: {earlier.AiOrderCheckSummary}");
            }

            var entry = review.DirectoryEntry;
            var url = OrderLinkFacts.ParseHttpUrl(review.OrderUrl);
            if (url == null)
            {
                Step("The order proof is not a web link.");
                Store(AiOrderCheckResult.CouldNotVerify, "The order proof is not a link that can be opened, so a person has to check it.");
                return 0;
            }

            var site = OrderLinkFacts.SiteKey(url.Host);
            if (OrderLinkFacts.IsNonOrderHost(url))
            {
                Step($"The proof link is on {site} (a picture, post or blockchain record), not an order page. Not opened.");
                Store(AiOrderCheckResult.CouldNotVerify, $"The proof is a link to {site} (a picture, post or blockchain record), not an order page on the listing's site. A person has to judge it.");
                return 0;
            }

            var listingLinks = new[] { entry.Link, entry.Link2, entry.Link3, entry.ProofLink };
            var onSite = OrderLinkFacts.IsOnListingSite(url, listingLinks);
            Step(onSite ? $"Order link is on the listing's own site ({site})." : $"Order link is on {site}, which is NOT one of the listing's addresses.");

            var normalized = OrderLinkFacts.NormalizeForCompare(review.OrderUrl);
            var sameHost = await this.db.DirectoryEntryReviews.AsNoTracking()
                .Where(r => r.DirectoryEntryReviewId != review.DirectoryEntryReviewId && r.OrderUrl != null && r.OrderUrl.Contains(url.Host))
                .Select(r => new { r.DirectoryEntryReviewId, r.OrderUrl, r.AuthorFingerprint, r.AuthorUserId })
                .ToListAsync(ct);
            var reused = sameHost.Where(r => OrderLinkFacts.NormalizeForCompare(r.OrderUrl) == normalized).OrderBy(r => r.DirectoryEntryReviewId).ToList();
            var reusedOther = reused.FirstOrDefault(r => r.AuthorFingerprint != review.AuthorFingerprint || r.AuthorUserId != review.AuthorUserId);
            var firstReuse = reusedOther ?? reused.FirstOrDefault();
            var facts = new LinkFacts(onSite, site, firstReuse?.DirectoryEntryReviewId, reusedOther != null);
            if (facts.ReusedOnReviewId is { } dupId)
            {
                Step($"Same order link also on review #{dupId} ({(facts.ReusedByOtherAuthor ? "different reviewer" : "same reviewer")}).");
            }

            var issueTags = tags.Where(t => !t.IsMoneyBand && t.Slug != ValidOrderSlug).Select(t => t.Name).ToList();
            var candidates = OrderLinkFacts.UnlockCandidates(review.OrderProofContext);

            // Fake-review signal: the same wallet address given as "verification details" by a
            // different reviewer. Real customers do not share a receiving address.
            string? sharedAddressNote = null;
            foreach (var address in candidates.Where(c => c.Length >= 26))
            {
                var others = await this.db.DirectoryEntryReviews.AsNoTracking()
                    .Where(r => r.DirectoryEntryReviewId != review.DirectoryEntryReviewId
                                && r.OrderProofContext != null && r.OrderProofContext.Contains(address)
                                && (r.AuthorFingerprint != review.AuthorFingerprint || r.AuthorUserId != review.AuthorUserId))
                    .Select(r => r.DirectoryEntryReviewId)
                    .OrderBy(id => id)
                    .Take(5)
                    .ToListAsync(ct);
                if (others.Count > 0)
                {
                    sharedAddressNote = $"The receiving address {address} was also given by a different reviewer on review {string.Join(", ", others.Select(o => "#" + o))}.";
                    Step("Warning: " + sharedAddressNote);
                    break;
                }
            }

            int calls = 0;
            string? filled = null;

            var page = await this.reader.ReadAsync(url, null, ct);
            Step(Describe(page, null));
            OrderVerdict? verdict = null;
            ClaudeResult? last = null;
            for (int round = 0; round < 2; round++)
            {
                last = await this.claude.CheckAsync(this.Dossier(review, entry, facts, page, filled, listingLinks) + (sharedAddressNote == null ? string.Empty : "\nSYSTEM WARNING (trusted): " + sharedAddressNote + " Treat this as a sign of a fake or coordinated review and say so in issues.\n"), page.Screenshot, issueTagNames: issueTags, ct);
                calls++;
                verdict = last.Json == null ? null : OrderVerdict.TryParse(last.Json, out _);
                if (verdict == null)
                {
                    var error = last.Error ?? "The AI answer could not be read.";
                    Step("AI call failed: " + error);
                    this.LogCall(review, entry, last, null, "Order check failed: AI call", error);
                    Store(AiOrderCheckResult.CouldNotVerify, "The check could not be completed (" + Truncate(error, 200) + "). Use \"Check again\" or check by hand.", model: last.Model);
                    return calls;
                }

                Step($"AI ({last.Model}, {last.InputTokens}/{last.OutputTokens} tokens): {verdict.Result}, status {verdict.OrderStatus}, confidence {verdict.Confidence:0.00}. {verdict.Summary}");

                // The page hides the order until a value is typed in: try the address (or code) the
                // reviewer supplied in the verification details, once.
                var wantsUnlock = verdict.NeedsUnlock || verdict.OrderStatus == OrderProofStatus.NeedsUnlockContext
                    || (!verdict.OrderVisible && page.Inputs.Count > 0 && page.Ok);
                if (round == 0 && wantsUnlock && !verdict.OrderVisible && candidates.Count > 0)
                {
                    this.LogCall(review, entry, last, verdict, "First look: page needs a value typed in");
                    filled = candidates[0];
                    page = await this.reader.ReadAsync(url, filled, ct);
                    Step(Describe(page, filled));
                    continue;
                }

                if (wantsUnlock && !verdict.OrderVisible && candidates.Count == 0)
                {
                    Step(entry.RequireReviewVerificationDetails
                        ? "The page asks for a value (such as the receiving address) and the verification details hold nothing usable."
                        : "The page asks for a value (such as the receiving address); the reviewer gave no verification details.");
                }

                break;
            }

            var decision = OrderCheckDecision.Decide(verdict!, facts);
            foreach (var note in decision.Notes)
            {
                Step("Note: " + note);
            }

            foreach (var issue in verdict!.Issues.Take(6))
            {
                Step("AI issue: " + issue);
            }

            decimal? usd = await this.UsdValueAsync(verdict!, Step, ct);
            var suggested = SuggestTags(decision.Result, verdict!, usd, tags);
            var current = review.ReviewTags.Select(t => t.ReviewTag.Name).ToList();
            var bandNow = review.ReviewTags.Select(t => t.ReviewTag).FirstOrDefault(t => t.IsMoneyBand);
            var bandSuggested = suggested.FirstOrDefault(n => tags.Any(t => t.IsMoneyBand && t.Name == n));
            if (bandNow != null && bandSuggested != null && bandNow.Name != bandSuggested)
            {
                Step($"Amount tag on the review is \"{bandNow.Name}\" but the order is about ${usd:0} → \"{bandSuggested}\".");
            }

            Step($"Tags now: {(current.Count > 0 ? string.Join(", ", current) : "none")}. Suggested: {(suggested.Count > 0 ? string.Join(", ", suggested) : "none")}.");

            var summary = decision.Summary;
            if (decision.Notes.Count > 0)
            {
                summary += " " + string.Join(" ", decision.Notes.Take(3));
            }

            if (sharedAddressNote != null)
            {
                summary += " WARNING: " + sharedAddressNote;
            }

            if (earlier != null && decision.Result == AiOrderCheckResult.CouldNotVerify)
            {
                Step("The page can no longer be read (order links expire), so the earlier result is kept.");
                Store(
                    AiOrderCheckResult.ChecksOut,
                    earlier.AiOrderCheckSummary?.Contains("(Seen on", StringComparison.Ordinal) == true
                        ? earlier.AiOrderCheckSummary
                        : $"{earlier.AiOrderCheckSummary} (Seen on {earlier.When:yyyy-MM-dd}; a later check could not read the page any more, which is normal once an order link expires.)",
                    earlier.AiOrderStatus,
                    earlier.AiOrderUsdValue,
                    earlier.AiSuggestedTags,
                    earlier.AiOrderCheckModel);
                review.AiOrderCheckDetails = earlier.AiOrderCheckDetails + "\n--- later check ---\n" + log;
                this.LogCall(review, entry, last!, verdict, "Order check → page gone; earlier ChecksOut kept");
                return calls;
            }

            Store(decision.Result, summary.Trim(), verdict!.OrderStatus, usd, string.Join(", ", suggested), last!.Model);
            this.LogCall(review, entry, last, verdict, $"Order check → {decision.Result}");
            return calls;
        }

        /// <summary>Tags the check suggests: "Valid Order" only for a checked order, one amount band, plus issue tags.</summary>
        public static List<string> SuggestTags(AiOrderCheckResult result, OrderVerdict verdict, decimal? usd, IReadOnlyList<ReviewTag> tags)
        {
            var suggested = new List<string>();
            if (result == AiOrderCheckResult.ChecksOut && tags.FirstOrDefault(t => t.Slug == ValidOrderSlug) is { } valid)
            {
                suggested.Add(valid.Name);
            }

            if (usd is { } value && verdict.OrderVisible && tags.FirstOrDefault(t => t.Matches(value)) is { } band)
            {
                suggested.Add(band.Name);
            }

            foreach (var name in verdict.SuggestedTags)
            {
                var tag = tags.FirstOrDefault(t => !t.IsMoneyBand && t.Slug != ValidOrderSlug && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (tag != null && !suggested.Contains(tag.Name))
                {
                    suggested.Add(tag.Name);
                }
            }

            return suggested;
        }

        private static string Describe(RenderedPage page, string? filled)
        {
            var sb = new StringBuilder(filled == null ? "Opened the order link" : "Opened the order link again and typed in the reviewer's value");
            if (page.Status is { } s)
            {
                sb.Append($" (HTTP {s})");
            }

            if (page.FinalUrl is { } f)
            {
                sb.Append($", ended at {Truncate(f, 160)}");
            }

            sb.Append($", {page.Text.Length} characters of text");
            if (page.FillResult is { } fr)
            {
                sb.Append($"; {fr}");
            }

            if (page.Error is { } e)
            {
                sb.Append($"; problem: {e}");
            }

            return sb.Append('.').ToString();
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

        private string Dossier(DirectoryEntryReview review, DirectoryEntry entry, LinkFacts facts, RenderedPage page, string? filled, IEnumerable<string?> listingLinks)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LISTING");
            sb.AppendLine($"Name: {entry.Name}");
            sb.AppendLine($"Addresses: {string.Join(", ", listingLinks.Where(l => !string.IsNullOrWhiteSpace(l)))}");
            sb.AppendLine();
            sb.AppendLine("REVIEW (untrusted text written by the reviewer)");
            var ageHours = Math.Max(0, (int)(DateTime.UtcNow - review.CreateDate).TotalHours);
            sb.AppendLine($"Written: {review.CreateDate:yyyy-MM-dd HH:mm} UTC, {ageHours} hours ago. Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.");
            var rating = review.Rating?.ToString(CultureInfo.InvariantCulture) ?? "none";
            sb.AppendLine($"Rating: {rating} of 5");
            if (!string.IsNullOrWhiteSpace(review.Title))
            {
                sb.AppendLine($"Title: {Truncate(review.Title, 200)}");
            }

            sb.AppendLine($"Text: {Truncate(review.Body ?? string.Empty, 2500)}");
            var chosen = review.ReviewTags.Select(t => t.ReviewTag.Name).ToList();
            sb.AppendLine($"Tags on the review now: {(chosen.Count > 0 ? string.Join(", ", chosen) : "none")}");
            sb.AppendLine($"Reviewer's verification details: {(string.IsNullOrWhiteSpace(review.OrderProofContext) ? "(none)" : Truncate(review.OrderProofContext, 800))}");
            sb.AppendLine();
            sb.AppendLine("FACTS CHECKED BY THE SYSTEM (trusted)");
            sb.AppendLine($"Order link: {review.OrderUrl}");
            sb.AppendLine(facts.OnListingSite
                ? "The order link is on one of the listing's own addresses."
                : $"The order link is on {facts.OrderSite}, which is NOT one of the listing's addresses.");
            if (facts.ReusedOnReviewId is { } id)
            {
                sb.AppendLine($"The same order link was used on review #{id} by {(facts.ReusedByOtherAuthor ? "a DIFFERENT reviewer" : "the same reviewer")}.");
            }

            var http = page.Status?.ToString(CultureInfo.InvariantCulture) ?? "no response";
            sb.AppendLine($"Page opened just now: HTTP {http}, ended at {page.FinalUrl ?? "(nowhere)"}, title \"{page.Title}\".");
            if (page.Error != null)
            {
                sb.AppendLine($"Problem opening it: {page.Error}");
            }

            if (page.Inputs.Count > 0)
            {
                sb.AppendLine($"Text boxes on the page: {string.Join(" | ", page.Inputs)}");
            }

            if (filled != null)
            {
                sb.AppendLine($"The system typed the reviewer's value \"{filled}\" into the page's box ({page.FillResult}). The text below is what the page showed afterwards.");
            }

            sb.AppendLine();
            sb.AppendLine("PAGE TEXT (untrusted)");
            sb.AppendLine(page.Text.Length == 0 ? "(the page showed no text)" : Truncate(page.Text, this.options.PageMaxChars));
            return sb.ToString();
        }

        private async Task<decimal?> UsdValueAsync(OrderVerdict v, Action<string> step, CancellationToken ct)
        {
            if (!v.OrderVisible)
            {
                return null;
            }

            if (v.UsdShown is { } shown && shown > 0 && shown <= MaxPlausibleUsd)
            {
                step($"Order value shown on the page: ${shown:0.##}.");
                return Math.Round(shown, 2);
            }

            var legs = new List<Tuple<decimal?, string?, string>>
            {
                Tuple.Create(v.SentAmount, v.SentCoin, "sent"),
                Tuple.Create(v.ReceivedAmount, v.ReceivedCoin, "received"),
            };
            foreach (var leg in legs)
            {
                // Amounts come from the page (through the AI), so absurd values are ignored rather
                // than multiplied: a huge number would overflow and crash the check.
                if (leg.Item1 is { } a && a > 0 && a <= MaxPlausibleAmount && !string.IsNullOrWhiteSpace(leg.Item2)
                    && await this.prices.UnitUsdAsync(leg.Item2, ct) is { } unit && unit > 0 && unit <= MaxPlausibleUnitUsd)
                {
                    var usd = Math.Round(a * unit, 2);
                    if (usd > MaxPlausibleUsd)
                    {
                        continue;
                    }

                    step($"Order value: {a} {PriceLookup.CleanTicker(leg.Item2)} {leg.Item3} × ${unit:0.####} (today's price) ≈ ${usd:0.##}.");
                    return usd;
                }
            }

            step("Order value could not be worked out (no amount or no price for the coin).");
            return null;
        }

        private void LogCall(DirectoryEntryReview review, DirectoryEntry entry, ClaudeResult result, OrderVerdict? verdict, string outcome, string? error = null)
        {
            this.db.AiCallLogs.Add(new AiCallLog
            {
                CreatedUtc = DateTime.UtcNow,
                Feature = AiCallLog.FeatureReviewOrderCheck,
                DirectoryEntryReviewId = review.DirectoryEntryReviewId,
                SubjectName = Truncate($"Review #{review.DirectoryEntryReviewId} of {entry.Name}", 255),
                Model = Truncate(result.Model ?? string.Empty, 100),
                Attempts = result.Attempts,
                HttpStatus = result.HttpStatus,
                DurationMs = result.DurationMs,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                StopReason = result.StopReason is { } sr ? Truncate(sr, 50) : null,
                Verdict = verdict?.Result,
                Confidence = verdict?.Confidence is { } c ? (double)c : null,
                Summary = verdict?.Summary,
                Outcome = Truncate(outcome, 200),
                Error = error ?? result.Error,
                SystemPromptChars = result.SystemPromptChars,
                RequestText = result.RequestText,
                ResponseText = result.Json,
            });
        }
    }
}
