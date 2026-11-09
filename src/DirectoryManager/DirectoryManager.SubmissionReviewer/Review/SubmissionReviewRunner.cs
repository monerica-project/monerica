using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Text;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.SubmissionReviewer.Review
{
    /// <summary>
    /// One pass of the reviewer: send stale previews for review, then review Pending submissions
    /// one at a time (pre-filter first, AI only when needed) within the daily and per-run caps.
    /// Nothing is ever published or rejected here; results wait in the admin review page.
    /// </summary>
    public sealed class SubmissionReviewRunner
    {
        private readonly ApplicationDbContext db;
        private readonly SubmissionDossierBuilder dossiers;
        private readonly IClaudeReviewService claude;
        private readonly ReviewerOptions options;

        public SubmissionReviewRunner(ApplicationDbContext db, SubmissionDossierBuilder dossiers, IClaudeReviewService claude, ReviewerOptions options)
        {
            this.db = db;
            this.dossiers = dossiers;
            this.claude = claude;
            this.options = options;
        }

        public async Task<int> RunAsync(CancellationToken ct)
        {
            await this.PromoteStalePreviewsAsync(ct);

            var todayUtc = DateTime.UtcNow.Date;
            var aiToday = await this.db.Submissions.CountAsync(s => s.ReviewedUtc >= todayUtc && s.ReviewModel != null, ct);
            var budget = Math.Min(this.options.PerRunCap, this.options.DailyCap - aiToday);

            var queue = await this.db.Submissions
                .Where(s => s.SubmissionStatus == SubmissionStatus.Pending && s.ReviewState == SubmissionReviewState.Pending)

                // Submissions people actually sent come first; drafts auto-sent from Preview after them.
                .OrderBy(s => s.ReviewLog != null && s.ReviewLog.Contains("Auto-submitted"))
                .ThenBy(s => s.SubmissionId)
                .Select(s => s.SubmissionId)
                .ToListAsync(ct);

            Console.WriteLine($"Queue: {queue.Count} pending; AI reviews today {aiToday}/{this.options.DailyCap}; this run may use {Math.Max(0, budget)}.");
            if (queue.Count == 0)
            {
                return 0;
            }

            var listings = await this.db.DirectoryEntries.AsNoTracking()
                .Select(d => new ExistingListing(d.DirectoryEntryId, d.Name, d.Link, d.Link2, d.Link3, d.DirectoryStatus == DirectoryStatus.Removed))
                .ToListAsync(ct);
            var otherLinks = (await this.db.Submissions.AsNoTracking()
                    .Where(s => s.SubmissionStatus == SubmissionStatus.Pending || s.SubmissionStatus == SubmissionStatus.Approved)
                    .Select(s => new { s.SubmissionId, s.Link, s.DirectoryEntryId })
                    .ToListAsync(ct))
                .Where(s => s.DirectoryEntryId == null)
                .Select(s => (s.SubmissionId, s.Link))
                .ToList();
            var subcats = await this.db.Subcategories.AsNoTracking()
                .Where(s => s.IsEnabled)
                .Select(s => new { s.SubCategoryId, Label = s.Category.Name + " > " + s.Name })
                .OrderBy(s => s.Label)
                .ToListAsync(ct);
            var names = subcats.Select(s => s.Label).ToList();
            var idsByName = subcats.GroupBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().SubCategoryId, StringComparer.OrdinalIgnoreCase);
            var labelById = subcats.ToDictionary(s => s.SubCategoryId, s => s.Label);
            var tagNames = await this.db.Tags.AsNoTracking().OrderBy(t => t.Name).Select(t => t.Name).ToListAsync(ct);

            // Style examples: descriptions of the most recently approved listings (newest first, stable per day).
            var examples = (await this.db.Submissions.AsNoTracking()
                    .Where(x => x.SubmissionStatus == SubmissionStatus.Approved && x.DirectoryEntryId != null)
                    .OrderByDescending(x => x.SubmissionId)
                    .Select(x => x.DirectoryEntryId!.Value)
                    .Take(60)
                    .ToListAsync(ct))
                .Distinct()
                .ToList();
            var styleExamples = (await this.db.DirectoryEntries.AsNoTracking()
                    .Where(d => examples.Contains(d.DirectoryEntryId) && d.Description != null && d.Description.Length >= 60
                                && (d.DirectoryStatus == DirectoryStatus.Admitted || d.DirectoryStatus == DirectoryStatus.Verified))
                    .OrderByDescending(d => d.DirectoryEntryId)
                    .Select(d => d.Name + " — " + d.Description)
                    .Take(10)
                    .ToListAsync(ct))
                .Select(x => x.Replace('\n', ' '))
                .ToList();
            var context = new ReviewContext(names, tagNames, styleExamples);

            int processed = 0;
            foreach (var id in queue)
            {
                var s = await this.db.Submissions.FirstAsync(x => x.SubmissionId == id, ct);
                s.OriginalSnapshotJson ??= s.CreateSnapshotJson();

                // Edit-submissions are compared with the listing itself, so they never count as duplicates of it;
                // other submissions for the same listing are handled by the web form's own checks.
                var linksForThis = s.DirectoryEntryId == null ? otherLinks : new List<(int, string)>();

                // Cheap checks that need no page fetch first (spam, duplicates, site-checker proposals).
                var decision = SubmissionPrefilter.Evaluate(s, listings, linksForThis, fetch: null);
                (FetchResult Main, FetchResult? Second)? pages = null;
                if (decision.Outcome == PrefilterOutcome.PassToAi)
                {
                    pages = await this.dossiers.FetchPagesAsync(s.Link, s.ProofLink, ct);
                    decision = SubmissionPrefilter.Evaluate(s, listings, linksForThis, pages.Value.Main);
                }

                s.ReviewPrefilterReason = Truncate(decision.ToString(), 500);
                s.ReviewError = null;
                Console.WriteLine($"#{s.SubmissionId} {s.Name}: prefilter {decision}");

                if (decision.Outcome != PrefilterOutcome.PassToAi)
                {
                    s.ReviewState = decision.Outcome switch
                    {
                        PrefilterOutcome.AutoReject => SubmissionReviewState.AutoRejected,
                        PrefilterOutcome.Skip => SubmissionReviewState.Skipped,
                        _ => SubmissionReviewState.Flagged,
                    };
                    s.ReviewBannedCategory = decision.Reason.StartsWith("Possible banned", StringComparison.Ordinal) ? true : s.ReviewBannedCategory;
                    s.ReviewedUtc = DateTime.UtcNow;
                    s.AppendReviewLog($"Prefilter → {s.ReviewState}: {decision.Reason}");
                    await this.db.SaveChangesAsync(ct);
                    continue;
                }

                if (processed >= budget)
                {
                    // Over the cap: stays Pending for the next run / next day (the admin page shows a notice).
                    await this.db.SaveChangesAsync(ct);
                    Console.WriteLine($"#{s.SubmissionId}: AI cap reached; left Pending.");
                    continue;
                }

                CurrentListing? current = null;
                if (s.DirectoryEntryId is int entryId)
                {
                    current = await this.db.DirectoryEntries.AsNoTracking()
                        .Where(d => d.DirectoryEntryId == entryId)
                        .Select(d => new CurrentListing(
                            d.DirectoryEntryId, d.Name, d.Link, d.Description, d.Note,
                            d.SubCategory != null ? d.SubCategory.Category.Name + " > " + d.SubCategory.Name : string.Empty,
                            d.DirectoryStatus.ToString(),
                            d.KycPolicy != null ? d.KycPolicy.ToString() : null,
                            d.CountryCode))
                        .FirstOrDefaultAsync(ct);
                }

                var similar = SubmissionDossierBuilder.MostSimilar(s, listings);
                var dossier = SubmissionDossierBuilder.Build(
                    s,
                    s.SubCategoryId is int sc && labelById.TryGetValue(sc, out var lbl) ? lbl : null,
                    pages!.Value.Main,
                    pages.Value.Second,
                    current,
                    similar,
                    this.options.DossierMaxChars);
                s.ReviewDossier = dossier;
                s.ReviewAttempts++;
                processed++;

                var result = await this.claude.ReviewAsync(dossier, context, ct);
                s.ReviewModel = result.Model;
                s.ReviewTokensIn = result.InputTokens;
                s.ReviewTokensOut = result.OutputTokens;
                s.ReviewedUtc = DateTime.UtcNow;
                s.ReviewVerdictJson = result.Json;

                if (result.Error != null)
                {
                    // Persistent failure: Flagged with the error, so nothing is silently dropped.
                    s.ReviewState = SubmissionReviewState.Flagged;
                    s.ReviewError = Truncate(result.Error, 2000);
                    s.AppendReviewLog($"AI review failed → Flagged: {result.Error}");
                    Console.WriteLine($"#{s.SubmissionId}: AI error: {result.Error}");
                    this.LogCall(s, result, null, "Submission flagged: AI call failed");
                    await this.db.SaveChangesAsync(ct);
                    continue;
                }

                var verdict = VerdictParser.TryParse(result.Json, out var parseError);
                if (verdict == null)
                {
                    s.ReviewState = SubmissionReviewState.Flagged;
                    s.ReviewError = Truncate("Could not use the AI verdict: " + parseError, 2000);
                    s.AppendReviewLog($"AI verdict unusable → Flagged: {parseError}");
                    this.LogCall(s, result, null, "Submission flagged: verdict unusable", "Could not use the AI verdict: " + parseError);
                    await this.db.SaveChangesAsync(ct);
                    continue;
                }

                ProposalMapper.Apply(s, verdict, idsByName);
                s.AppendReviewLog($"AI ({result.Model}, {result.InputTokens}/{result.OutputTokens} tokens): {verdict.Verdict} {verdict.Confidence:0.00} → {s.ReviewState}. {verdict.Summary}");
                Console.WriteLine($"#{s.SubmissionId}: {verdict.Verdict} ({verdict.Confidence:0.00}) → {s.ReviewState}");
                this.LogCall(s, result, verdict, $"Submission → {s.ReviewState}; suggested edits saved for admin review");
                await this.db.SaveChangesAsync(ct);
            }

            return processed;
        }

        /// <summary>
        /// Drafts left in Preview longer than the configured time are sent for review as if the
        /// submitter had pressed Submit (the web form's own duplicate and blocked-domain checks
        /// are repeated here; drafts that fail them stay in Preview).
        /// </summary>
        public async Task<int> PromoteStalePreviewsAsync(CancellationToken ct)
        {
            var cutoff = DateTime.UtcNow.AddHours(-this.options.PreviewPromoteAfterHours);
            var since = this.options.PreviewPromoteSinceUtc;
            var stale = await this.db.Submissions
                .Where(s => s.SubmissionStatus == SubmissionStatus.Preview
                            && (s.UpdateDate ?? s.CreateDate) < cutoff
                            && s.CreateDate >= since)
                .OrderBy(s => s.SubmissionId)
                .ToListAsync(ct);
            if (stale.Count == 0)
            {
                return 0;
            }

            var waiting = await this.db.Submissions.AsNoTracking()
                .Where(s => s.SubmissionStatus == SubmissionStatus.Pending)
                .Select(s => new { s.SubmissionId, s.Link, s.DirectoryEntryId })
                .ToListAsync(ct);
            var blocked = (await this.db.SubmissionBlockedDomains.AsNoTracking()
                    .Where(b => b.IsEnabled)
                    .Select(b => b.Domain)
                    .ToListAsync(ct))
                .Select(d => UrlNormalizer.Host(d))
                .Where(d => d.Length > 0)
                .ToList();

            int promoted = 0;
            foreach (var s in stale)
            {
                var key = UrlNormalizer.Normalize(s.Link);
                var dup = waiting.FirstOrDefault(w => w.SubmissionId != s.SubmissionId &&
                    ((s.DirectoryEntryId != null && w.DirectoryEntryId == s.DirectoryEntryId) || UrlNormalizer.Normalize(w.Link) == key));
                if (dup != null)
                {
                    continue;
                }

                var host = UrlNormalizer.Host(s.Link);
                if (blocked.Any(b => host == b || host.EndsWith("." + b, StringComparison.Ordinal)))
                {
                    continue;
                }

                s.SubmissionStatus = SubmissionStatus.Pending;
                s.ReviewState = SubmissionReviewState.Pending;
                s.AppendReviewLog($"Auto-submitted: left in Preview for over {this.options.PreviewPromoteAfterHours}h (submitter never pressed Submit).");
                waiting.Add(new { s.SubmissionId, s.Link, s.DirectoryEntryId });
                promoted++;
            }

            await this.db.SaveChangesAsync(ct);
            Console.WriteLine($"Previews: {stale.Count} older than {this.options.PreviewPromoteAfterHours}h, {promoted} sent for review.");
            return promoted;
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        /// <summary>Records one AI call in the permanent AI activity log (admin: /admin/ai-log).</summary>
        private void LogCall(Submission s, ClaudeResult result, AiVerdict? verdict, string outcome, string? extraError = null)
        {
            this.db.AiCallLogs.Add(new AiCallLog
            {
                CreatedUtc = DateTime.UtcNow,
                Feature = AiCallLog.FeatureSubmissionReview,
                SubmissionId = s.SubmissionId,
                SubjectName = s.Name is { } n ? Truncate(n, 255) : null,
                Model = Truncate(result.Model ?? string.Empty, 100),
                Attempts = result.Attempts,
                HttpStatus = result.HttpStatus,
                DurationMs = result.DurationMs,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                StopReason = result.StopReason is { } sr ? Truncate(sr, 50) : null,
                Verdict = verdict?.Verdict is { } v ? Truncate(v, 50) : null,
                Confidence = verdict?.Confidence is { } c ? (double)c : null,
                Summary = verdict?.Summary,
                Outcome = Truncate(outcome, 200),
                Error = result.Error ?? extraError,
                SystemPromptChars = result.SystemPromptChars,
                RequestText = result.RequestText,
                ResponseText = result.Json,
            });
        }
    }
}
