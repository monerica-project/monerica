using System.Text;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Text;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.TermsWatcher.Watch
{
    /// <summary>What was read from one listing's site in this run.</summary>
    public sealed record SiteReading(bool Readable, IReadOnlyList<string> Pages, IReadOnlyList<string> Sentences, string? Problem);

    public sealed record RunTotals(int Checked, int Baselines, int Unchanged, int Changed, int Flagged, int NotReviewed, int Unreadable, int AiCalls);

    /// <summary>
    /// Monthly check of every live listing's own policy pages. For each listing: read the home
    /// page plus up to two terms/FAQ/AML pages, keep only the sentences about ID checks, countries,
    /// VPN/Tor and fees, and compare them with last month's. No change = no cost. A change is sent
    /// to the AI (within the monthly budget) with the listing's current data; if the data looks
    /// wrong the row is marked for the owner on /admin/terms-watch. A listing is never edited here.
    /// </summary>
    public sealed class TermsWatchRunner
    {
        private readonly ApplicationDbContext db;
        private readonly IPageFetcher fetcher;
        private readonly IClaudeTermsService claude;
        private readonly TermsWatchOptions options;

        public TermsWatchRunner(ApplicationDbContext db, IPageFetcher fetcher, IClaudeTermsService claude, TermsWatchOptions options)
        {
            this.db = db;
            this.fetcher = fetcher;
            this.claude = claude;
            this.options = options;
        }

        public async Task<RunTotals> RunAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var aiUsed = await this.db.AiCallLogs.CountAsync(l => l.Feature == AiCallLog.FeatureTermsWatch && l.CreatedUtc >= monthStart, ct);
            var aiAvailable = !string.IsNullOrWhiteSpace(this.options.ApiKey);
            var dueBefore = now.AddDays(-this.options.MinDaysBetweenChecks);

            var due = await this.db.DirectoryEntries.AsNoTracking()
                .Where(e => e.DirectoryStatus == DirectoryStatus.Admitted || e.DirectoryStatus == DirectoryStatus.Verified)
                .Where(e => !this.db.ListingTermsSnapshots.Any(s => s.DirectoryEntryId == e.DirectoryEntryId && s.LastCheckedUtc > dueBefore))
                .OrderBy(e => e.DirectoryEntryId)
                .Take(this.options.MaxListingsPerRun)
                .ToListAsync(ct);
            Console.WriteLine($"{due.Count} listing(s) due. AI calls used this month: {aiUsed}/{this.options.MonthlyAiCap}.");

            int baselines = 0, unchanged = 0, changed = 0, flagged = 0, notReviewed = 0, unreadable = 0, aiCalls = 0, done = 0;
            foreach (var batch in due.Chunk(Math.Max(1, this.options.Concurrency) * 6))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                // Fetching is the slow part: do a batch at once, then write results one by one.
                using var gate = new SemaphoreSlim(Math.Max(1, this.options.Concurrency));
                var readings = await Task.WhenAll(batch.Select(async e =>
                {
                    await gate.WaitAsync(ct);
                    try
                    {
                        return (Entry: e, Reading: await this.ReadSiteAsync(e, ct));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        return (Entry: e, Reading: new SiteReading(false, Array.Empty<string>(), Array.Empty<string>(), ex.Message));
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));

                foreach (var (entry, reading) in readings)
                {
                    var snap = await this.db.ListingTermsSnapshots.FirstOrDefaultAsync(s => s.DirectoryEntryId == entry.DirectoryEntryId, ct);
                    var isNew = snap == null;
                    snap ??= new ListingTermsSnapshot { DirectoryEntryId = entry.DirectoryEntryId };
                    if (isNew)
                    {
                        this.db.ListingTermsSnapshots.Add(snap);
                    }

                    var oldSentences = Lines(snap.Sentences);
                    snap.LastCheckedUtc = DateTime.UtcNow;

                    // Nothing readable now but there was text before: almost always a fetch problem
                    // (site down, bot wall), not a policy change. Keep last month's text and move on.
                    if (!reading.Readable || (reading.Sentences.Count == 0 && oldSentences.Count > 0))
                    {
                        snap.ReadFailures++;
                        if (isNew || snap.Fingerprint == null)
                        {
                            snap.Result = TermsWatchResult.CouldNotRead;
                            snap.Summary = Truncate("Could not read the site: " + (reading.Problem ?? "no policy text found"), 2000);
                        }

                        unreadable++;
                    }
                    else
                    {
                        var set = TermsSignalText.ComparisonSet(reading.Sentences);
                        var fingerprint = TermsSignalText.Fingerprint(set);
                        var hadBaseline = snap.Fingerprint != null;
                        snap.ReadFailures = 0;
                        snap.LastReadUtc = DateTime.UtcNow;
                        snap.Pages = string.Join("\n", reading.Pages);

                        if (!hadBaseline)
                        {
                            snap.Result = TermsWatchResult.Baseline;
                            snap.Summary = null;
                            baselines++;
                        }
                        else if (fingerprint == snap.Fingerprint)
                        {
                            if (!snap.NeedsReview)
                            {
                                snap.Result = TermsWatchResult.Unchanged;
                            }

                            unchanged++;
                        }
                        else
                        {
                            var (added, removed) = TermsSignalText.Diff(oldSentences, reading.Sentences);
                            if (added.Count == 0 && removed.Count == 0)
                            {
                                // Only the order or the 80-sentence window moved.
                                unchanged++;
                            }
                            else
                            {
                                changed++;
                                snap.ChangedUtc = DateTime.UtcNow;
                                snap.Diff = Truncate(string.Join("\n", added.Take(40).Select(s => "+ " + s).Concat(removed.Take(40).Select(s => "- " + s))), 30000);
                                snap.Suggestion = null;
                                snap.HandledUtc = null;

                                if (aiAvailable && aiUsed < this.options.MonthlyAiCap)
                                {
                                    var request = this.BuildRequest(entry, added, removed);
                                    var ai = await this.claude.CompareAsync(request, ct);
                                    aiUsed++;
                                    aiCalls++;
                                    var verdict = TermsVerdict.TryParse(ai.Json);
                                    this.LogCall(entry, ai, verdict, request);
                                    if (ai.OutOfCredit)
                                    {
                                        aiAvailable = false;
                                        Console.WriteLine("AI stopped for this run: the API key was refused or has no credit left. " + ai.Error);
                                    }

                                    if (verdict == null)
                                    {
                                        snap.Result = TermsWatchResult.ChangedNotReviewed;
                                        snap.Summary = "The policy wording changed, but the AI comparison failed. Please look at the changed sentences.";
                                        snap.NeedsReview = true;
                                        notReviewed++;
                                    }
                                    else if (verdict.NeedsUpdate)
                                    {
                                        snap.Result = TermsWatchResult.Flagged;
                                        snap.Summary = Truncate(verdict.Summary, 2000);
                                        snap.Suggestion = Truncate(string.Join("\n", verdict.Suggestions), 2000);
                                        snap.NeedsReview = true;
                                        flagged++;
                                    }
                                    else
                                    {
                                        snap.Result = TermsWatchResult.ChangedNoAction;
                                        snap.Summary = Truncate(verdict.Summary, 2000);
                                        snap.NeedsReview = false;
                                    }
                                }
                                else
                                {
                                    snap.Result = TermsWatchResult.ChangedNotReviewed;
                                    snap.Summary = aiAvailable
                                        ? "The policy wording changed. The monthly AI budget is used up, so this one was not compared automatically."
                                        : "The policy wording changed. The AI comparison was not available, so this one was not compared automatically.";
                                    snap.NeedsReview = true;
                                    notReviewed++;
                                }
                            }
                        }

                        // This month's text becomes the basis for next month's comparison.
                        snap.Sentences = Truncate(string.Join("\n", reading.Sentences.Take(TermsSignalText.MaxSentences)), 40000);
                        snap.Fingerprint = fingerprint;
                    }

                    await this.db.SaveChangesAsync(ct);
                    done++;
                }

                Console.WriteLine($"  {done}/{due.Count} checked ({changed} changed, {flagged} flagged, {unreadable} unreadable, {aiCalls} AI calls)");
            }

            return new RunTotals(done, baselines, unchanged, changed, flagged, notReviewed, unreadable, aiCalls);
        }

        /// <summary>Home page plus up to two same-site terms/FAQ/AML pages; onion-only listings go through Tor.</summary>
        public async Task<SiteReading> ReadSiteAsync(DirectoryEntry entry, CancellationToken ct)
        {
            var start = new[] { entry.Link, entry.Link2, entry.Link3 }
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l) && !UrlNormalizer.IsOnion(l))
                ?? new[] { entry.Link, entry.Link2, entry.Link3 }.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            if (string.IsNullOrWhiteSpace(start) || !Uri.TryCreate(start.Trim(), UriKind.Absolute, out _))
            {
                return new SiteReading(false, Array.Empty<string>(), Array.Empty<string>(), "the listing has no usable link");
            }

            var home = await this.fetcher.FetchAsync(start.Trim(), ct);
            if (!home.Reachable || string.IsNullOrWhiteSpace(home.Html))
            {
                return new SiteReading(false, Array.Empty<string>(), Array.Empty<string>(), home.Blocked ? $"the site refused the request (HTTP {home.StatusCode})" : home.Error ?? $"HTTP {home.StatusCode}");
            }

            var pages = new List<string> { start.Trim() };
            var sentences = new List<string>();
            sentences.AddRange(TermsSignalText.RelevantSentences(HtmlText.Extract(home.Html, 60000).Body));

            var baseUri = home.FinalUri ?? new Uri(start.Trim());
            foreach (var url in SiteSignalScan.LegalPages(home.Html, baseUri, new[] { entry.Link2, entry.Link3, entry.ProofLink }, max: 2))
            {
                var page = await this.fetcher.FetchAsync(url, ct);
                if (page.Reachable && !string.IsNullOrWhiteSpace(page.Html))
                {
                    pages.Add(url);
                    sentences.AddRange(TermsSignalText.RelevantSentences(HtmlText.Extract(page.Html, 60000).Body));
                }
            }

            var distinct = sentences
                .GroupBy(TermsSignalText.Normalize, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(TermsSignalText.Normalize, StringComparer.Ordinal)
                .Take(TermsSignalText.MaxSentences)
                .ToList();
            return new SiteReading(true, pages, distinct, distinct.Count == 0 ? "no policy text found (the site may need JavaScript)" : null);
        }

        public string BuildRequest(DirectoryEntry e, IReadOnlyList<string> added, IReadOnlyList<string> removed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LISTING (current data)");
            sb.AppendLine($"Name: {e.Name}");
            sb.AppendLine($"Site: {e.Link}");
            sb.AppendLine($"Description: {Truncate(e.Description ?? string.Empty, 400)}");
            sb.AppendLine($"Public note: {(string.IsNullOrWhiteSpace(e.Note) ? "(none)" : Truncate(e.Note, 400))}");
            var kyc = e.KycPolicy?.ToString() ?? "NotStated";
            sb.AppendLine($"KYC policy: {kyc}");
            sb.AppendLine($"Restricted countries: {(string.IsNullOrWhiteSpace(e.RestrictedCountries) ? "(none recorded)" : e.RestrictedCountries)}");
            sb.AppendLine($"VPN policy: {e.VpnPolicy}");
            sb.AppendLine($"Tor policy: {e.TorPolicy}");
            sb.AppendLine($"Other restrictions: {(string.IsNullOrWhiteSpace(e.OtherRestrictions) ? "(none)" : e.OtherRestrictions)}");
            sb.AppendLine();
            sb.AppendLine("SENTENCES ADDED to the site's pages since last month (untrusted):");
            sb.AppendLine(added.Count == 0 ? "(none)" : string.Join("\n", added.Take(this.options.MaxDiffSentences).Select(s => "+ " + s)));
            sb.AppendLine();
            sb.AppendLine("SENTENCES REMOVED since last month (untrusted):");
            sb.AppendLine(removed.Count == 0 ? "(none)" : string.Join("\n", removed.Take(this.options.MaxDiffSentences).Select(s => "- " + s)));
            return sb.ToString();
        }

        private static List<string> Lines(string? text) =>
            (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

        private void LogCall(DirectoryEntry entry, TermsAiResult result, TermsVerdict? verdict, string request)
        {
            this.db.AiCallLogs.Add(new AiCallLog
            {
                CreatedUtc = DateTime.UtcNow,
                Feature = AiCallLog.FeatureTermsWatch,
                SubjectName = Truncate($"Terms check: {entry.Name} (listing {entry.DirectoryEntryId})", 255),
                Model = Truncate(result.Model ?? string.Empty, 100),
                Attempts = result.Attempts,
                HttpStatus = result.HttpStatus,
                DurationMs = result.DurationMs,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                StopReason = result.StopReason,
                Verdict = verdict == null ? null : (verdict.NeedsUpdate ? "needs_update" : "no_action"),
                Confidence = verdict?.Confidence is { } c ? (double)c : null,
                Summary = verdict?.Summary,
                Outcome = verdict == null ? "Terms check: AI call failed" : (verdict.NeedsUpdate ? "Terms check → flagged for the owner" : "Terms check → no action"),
                Error = result.Error,
                SystemPromptChars = result.SystemPromptChars,
                RequestText = request,
                ResponseText = result.Json,
            });
        }
    }
}
