using System.Text.Json;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.GuaranteeSync
{
    /// <summary>One service in OrangeFren's guarantee feed.</summary>
    public sealed record FeedService(string Name, decimal Btc);

    public sealed record SyncChange(string Listing, string Kind, decimal? OldBtc, decimal? NewBtc);

    public sealed record SyncResult(IReadOnlyList<SyncChange> Changes, IReadOnlyList<string> Unmatched, IReadOnlyList<string> NotInFeed, string? Problem);

    /// <summary>
    /// Keeps the "deposit guarantee held by OrangeFren" row on Monerica listings in step with
    /// OrangeFren's feed: updates the BTC amount when it changed and adds the row when a listed
    /// service newly appears in the feed. It never deletes a row and never touches a listing's
    /// other guarantees (CypherGoat, AntiKYC, forum escrows...). A listing that has an OrangeFren
    /// row but is missing from the feed is only reported, because the feed has been seen to list
    /// fewer services than OrangeFren's public page.
    /// </summary>
    public static class OrangeFrenGuarantees
    {
        public static List<FeedService>? Parse(string json, out string? problem)
        {
            problem = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("status", out var st) || st.GetString() != "ok")
                {
                    problem = "the feed did not say status ok";
                    return null;
                }

                var list = new List<FeedService>();
                foreach (var s in root.GetProperty("services").EnumerateArray())
                {
                    var name = s.TryGetProperty("service_name", out var n) ? n.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name)
                        && s.TryGetProperty("guarantee_btc", out var b) && b.ValueKind == JsonValueKind.Number
                        && b.TryGetDecimal(out var btc) && btc > 0 && btc < 1000)
                    {
                        list.Add(new FeedService(name.Trim(), Math.Round(btc, 8)));
                    }
                }

                return list;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                problem = "the feed could not be read: " + ex.Message;
                return null;
            }
        }

        /// <summary>"Pablo.Cash", "pablo-cash" and "pablocash" all compare equal.</summary>
        public static string Squash(string? value) =>
            new string((value ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static bool IsOrangeFrenLink(string? link) =>
            Uri.TryCreate(link, UriKind.Absolute, out var u)
            && (u.Host.Equals("orangefren.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".orangefren.com", StringComparison.OrdinalIgnoreCase));

        public static async Task<SyncResult> ApplyAsync(ApplicationDbContext db, IReadOnlyList<FeedService> feed, GuaranteeSyncOptions options, CancellationToken ct)
        {
            if (feed.Count < options.MinServices)
            {
                return new SyncResult(Array.Empty<SyncChange>(), Array.Empty<string>(), Array.Empty<string>(), $"the feed lists only {feed.Count} service(s); expected at least {options.MinServices}. Nothing was changed.");
            }

            // Removed listings are skipped; every other status keeps its guarantee current
            // (a Questionable or Scam listing still shows what deposit exists).
            var entries = await db.DirectoryEntries.AsNoTracking()
                .Where(e => e.DirectoryStatus != DirectoryStatus.Removed)
                .Select(e => new { e.DirectoryEntryId, e.DirectoryEntryKey, e.Name })
                .ToListAsync(ct);
            var byKey = entries.GroupBy(e => Squash(e.DirectoryEntryKey)).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            var byName = entries.GroupBy(e => Squash(e.Name)).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());

            var changes = new List<SyncChange>();
            var unmatched = new List<string>();
            var matchedEntryIds = new HashSet<int>();
            var changedKeys = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var service in feed)
            {
                var wanted = Squash(options.Aliases.TryGetValue(service.Name, out var alias) ? alias : service.Name);
                var entry = byKey.GetValueOrDefault(wanted) ?? byName.GetValueOrDefault(wanted);
                if (entry == null)
                {
                    unmatched.Add(service.Name);
                    continue;
                }

                matchedEntryIds.Add(entry.DirectoryEntryId);
                var rows = await db.DirectoryEntryGuarantees.Where(g => g.DirectoryEntryId == entry.DirectoryEntryId).ToListAsync(ct);
                var row = rows.FirstOrDefault(g => IsOrangeFrenLink(g.Link));
                if (row == null)
                {
                    db.DirectoryEntryGuarantees.Add(new DirectoryEntryGuarantee
                    {
                        DirectoryEntryId = entry.DirectoryEntryId,
                        SortOrder = rows.Count == 0 ? 1 : rows.Max(g => g.SortOrder) + 1,
                        Link = options.ProofLink,
                        Amount = service.Btc,
                        Currency = Currency.BTC,
                        CreateDate = now,
                        CreatedByUserId = "guarantee-sync",
                    });
                    changes.Add(new SyncChange(entry.Name, "added", null, service.Btc));
                    changedKeys.Add(entry.DirectoryEntryKey);
                }
                else if (row.Currency != Currency.BTC || Math.Round(row.Amount, 8) != service.Btc)
                {
                    changes.Add(new SyncChange(entry.Name, "updated", row.Currency == Currency.BTC ? row.Amount : null, service.Btc));
                    row.Amount = service.Btc;
                    row.Currency = Currency.BTC;
                    row.UpdateDate = now;
                    row.UpdatedByUserId = "guarantee-sync";
                    changedKeys.Add(entry.DirectoryEntryKey);
                }
            }

            // Listings that show an OrangeFren guarantee the feed no longer mentions: report only.
            var withRow = await db.DirectoryEntryGuarantees.AsNoTracking()
                .Where(g => g.Link.Contains("orangefren.com"))
                .Select(g => g.DirectoryEntryId)
                .Distinct()
                .ToListAsync(ct);
            var notInFeed = entries.Where(e => withRow.Contains(e.DirectoryEntryId) && !matchedEntryIds.Contains(e.DirectoryEntryId))
                .Select(e => e.Name).OrderBy(n => n).ToList();

            if (changes.Count > 0)
            {
                await db.SaveChangesAsync(ct);

                // The static mirror (monerica.com) republishes the changed listing pages.
                await new StaticPublishRepository(db).EnqueueAsync(
                    changedKeys.Distinct().Select(k => "/site/" + k), "deposit guarantee updated from OrangeFren", "instant", now, ct);
            }

            return new SyncResult(changes, unmatched, notInFeed, null);
        }
    }
}
