using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.TrustPages;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>Rules behind /reviews, /incidents and the "New" listing label.</summary>
    public class TrustPagesRulesTests
    {
        private static readonly DateTime Now = new (2026, 11, 11, 15, 30, 0, DateTimeKind.Utc);

        // ---------------- "New" label (90 days) ----------------
        [Fact]
        public void NewLabel_LastsNinetyWholeUtcDays_AndEndsAtMidnight()
        {
            var added = new DateTime(2026, 8, 13, 23, 50, 0, DateTimeKind.Utc);
            var ends = NewListingRule.EndsUtc(added);

            Assert.Equal(90, NewListingRule.Days);
            Assert.Equal(new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), ends);
            Assert.True(NewListingRule.IsNew(added, DirectoryStatus.Admitted, ends.AddTicks(-1)));
            Assert.False(NewListingRule.IsNew(added, DirectoryStatus.Admitted, ends));
            Assert.True(NewListingRule.IsNew(Now.AddDays(-89), DirectoryStatus.Verified, Now));
            Assert.False(NewListingRule.IsNew(Now.AddDays(-91), DirectoryStatus.Verified, Now));
        }

        [Theory]
        [InlineData(DirectoryStatus.Admitted, true)]
        [InlineData(DirectoryStatus.Verified, true)]
        [InlineData(DirectoryStatus.Questionable, true)]
        [InlineData(DirectoryStatus.Scam, false)]
        [InlineData(DirectoryStatus.Removed, false)]
        [InlineData(DirectoryStatus.Unknown, false)]
        public void NewLabel_OnlyForPubliclyUsableStatuses(DirectoryStatus status, bool expected)
        {
            Assert.Equal(expected, NewListingRule.IsNew(Now.AddDays(-1), status, Now));
        }

        [Fact]
        public void NewLabel_NeverForAMissingDate()
        {
            Assert.False(NewListingRule.IsNew(DateTime.MinValue, DirectoryStatus.Admitted, Now));
        }

        [Fact]
        public void LatestEnd_IsTheNewestPastMidnight_IgnoringFutureEndsAndIneligibleListings()
        {
            var listings = new[]
            {
                (Now.AddDays(-200), DirectoryStatus.Admitted),     // ended long ago
                (Now.AddDays(-91), DirectoryStatus.Verified),      // ended yesterday or today
                (Now.AddDays(-10), DirectoryStatus.Admitted),      // still new: in the future
                (Now.AddDays(-90), DirectoryStatus.Removed),       // not eligible
            };

            var latest = NewListingRule.LatestEndAtOrBefore(listings, Now);

            Assert.Equal(NewListingRule.EndsUtc(Now.AddDays(-91)), latest);
            Assert.True(latest <= Now);
            Assert.Null(NewListingRule.LatestEndAtOrBefore(new[] { (Now.AddDays(-5), DirectoryStatus.Admitted) }, Now));
        }

        [Fact]
        public void NewBadge_RendersInListingRows_ButNotForOldListingsOrEmails()
        {
            var fresh = Row(DateTime.UtcNow.AddDays(-3));
            var old = Row(DateTime.UtcNow.AddDays(-120));
            var email = Row(DateTime.UtcNow.AddDays(-3));
            email.ItemDisplayType = ItemDisplayType.Email;

            Assert.Contains(">New</span>", DisplayMarkUpHelper.GenerateDirectoryEntryHtml(fresh, "https://dir.example"));
            Assert.DoesNotContain(">New</span>", DisplayMarkUpHelper.GenerateDirectoryEntryHtml(old, "https://dir.example"));
            Assert.DoesNotContain(">New</span>", DisplayMarkUpHelper.GenerateDirectoryEntryHtml(email, "https://dir.example"));
            Assert.Contains(">New</span>", DisplayMarkUpHelper.GenerateSearchResultHtml(fresh, "https://dir.example"));
            Assert.Equal(string.Empty, DisplayMarkUpHelper.NewBadgeHtml(Now.AddDays(-3), DirectoryStatus.Scam, Now));
        }

        // ---------------- /reviews filters ----------------
        [Fact]
        public void ReviewFilter_JunkValuesFallBackToDefaults()
        {
            var f = PublicReviewFilter.Parse("  ", "nope", "9", "made-up", "maybe", "sideways", "-4", Cats(), Tags());

            Assert.True(f.IsDefault);
            Assert.Null(f.CategoryId);
            Assert.Null(f.Rating);
            Assert.Null(f.TagId);
            Assert.False(f.WithReplies);
            Assert.Equal(PublicReviewSort.Newest, f.Sort);
            Assert.Equal(1, f.Page);
            Assert.Equal(string.Empty, f.ToQueryString());
        }

        [Fact]
        public void ReviewFilter_ParsesKnownValues_AndBuildsAStableQueryString()
        {
            var f = PublicReviewFilter.Parse("fast swap", "Exchanges", "1", "FUNDS-HELD", "1", "lowest", "3", Cats(), Tags());

            Assert.Equal("fast swap", f.Search);
            Assert.Equal(1, f.CategoryId);
            Assert.Equal((byte)1, f.Rating);
            Assert.Equal(15, f.TagId);
            Assert.True(f.WithReplies);
            Assert.Equal(PublicReviewSort.Lowest, f.Sort);
            Assert.Equal(3, f.Page);
            Assert.False(f.IsDefault);
            Assert.Equal("?q=fast%20swap&category=exchanges&rating=1&tag=funds-held&replies=1&sort=lowest&page=3", f.ToQueryString());
            Assert.Equal("?q=fast%20swap&category=exchanges&rating=1&tag=funds-held&replies=1&sort=lowest", f.ToQueryString(1));
        }

        [Fact]
        public void ReviewFilter_PageIsClampedToAPositiveCappedNumber()
        {
            Assert.Equal(1, PublicReviewFilter.ParsePage("0"));
            Assert.Equal(1, PublicReviewFilter.ParsePage("abc"));
            Assert.Equal(1, PublicReviewFilter.ParsePage("99999999999999"));
            Assert.Equal(PublicReviewFilter.MaxPage, PublicReviewFilter.ParsePage("50000"));
            Assert.Equal(7, PublicReviewFilter.ParsePage(" 7 "));
            Assert.Equal(1, PublicReviewFilter.Parse(null, null, null, null, null, null, "1", Cats(), Tags()).Page);
        }

        [Fact]
        public void ReviewFilter_SearchIsCleanedAndCappedAtOneHundredCharacters()
        {
            Assert.Equal("a b", PublicReviewFilter.CleanSearch("  a \t\n b  "));
            Assert.Equal("ab", PublicReviewFilter.CleanSearch("a\u0000b"));
            Assert.Null(PublicReviewFilter.CleanSearch("\u0001 "));

            var longText = new string('x', 150);
            Assert.Equal(100, PublicReviewFilter.CleanSearch(longText)!.Length);

            // A surrogate pair straddling the cut is dropped whole, never split.
            var emoji = new string('x', 99) + "\U0001F600";
            var cleaned = PublicReviewFilter.CleanSearch(emoji)!;
            Assert.Equal(99, cleaned.Length);
            Assert.False(char.IsHighSurrogate(cleaned[^1]));

            // Wildcards are kept as text (the repository escapes them for ILIKE).
            Assert.Equal("100%_off", PublicReviewFilter.CleanSearch("100%_off"));
            Assert.Equal("?q=100%25_off", new PublicReviewFilter { Search = "100%_off" }.ToQueryString());
        }

        // ---------------- /incidents filters ----------------
        [Fact]
        public void IncidentFilter_ReviewWindowDefaultsToNinetyDays_AndPagesOnlySingleKindViews()
        {
            var all = IncidentFilter.Parse(null, null, "4");
            Assert.True(all.IsDefault);
            Assert.Equal(IncidentFilter.DefaultWindowDays, all.WindowDays);
            Assert.Equal(1, all.Page);
            Assert.Equal(string.Empty, all.ToQueryString());
            Assert.Equal(Now.AddDays(-90), all.SinceUtc(Now));

            var reviews = IncidentFilter.Parse("REVIEWS", "all", "2");
            Assert.Equal(IncidentKind.Reviews, reviews.Kind);
            Assert.Null(reviews.WindowDays);
            Assert.Null(reviews.SinceUtc(Now));
            Assert.Equal("?kind=reviews&days=all&page=2", reviews.ToQueryString());

            // The window is never written for the open-issues view (it doesn't apply there).
            var status = IncidentFilter.Parse("status", "30", "3");
            Assert.Equal("?kind=status&page=3", status.ToQueryString());

            var junk = IncidentFilter.Parse("everything", "45", "x");
            Assert.Equal(IncidentKind.All, junk.Kind);
            Assert.Equal(90, junk.WindowDays);
            Assert.Equal("?kind=reviews", junk.ToQueryString(IncidentKind.Reviews, 1));
        }

        // ---------------- open issues ----------------
        [Fact]
        public void OpenIssues_UseTheDateAndNoteFromWhenTheCurrentStatusBegan()
        {
            var issues = OpenIssueBuilder.Build(
                new[]
                {
                    A(1, 7, DirectoryStatus.Admitted, -100),
                    A(2, 7, DirectoryStatus.Scam, -20, "Took deposits and stopped answering"),
                    A(3, 7, DirectoryStatus.Scam, -5, "edited later"),
                },
                new[] { L(7, DirectoryStatus.Scam, "edited later") });

            var i = Assert.Single(issues);
            Assert.Equal(DirectoryStatus.Scam, i.Status);
            Assert.Equal(Now.AddDays(-20), i.SinceUtc);
            Assert.Equal("Took deposits and stopped answering", i.Reason);
        }

        [Fact]
        public void OpenIssues_ShowOnlyTheCurrentStatus_SinceItWasEntered()
        {
            var issues = OpenIssueBuilder.Build(
                new[]
                {
                    A(1, 7, DirectoryStatus.Questionable, -300, "doubts"),
                    A(2, 7, DirectoryStatus.Admitted, -200),
                    A(3, 7, DirectoryStatus.Scam, -100, "funds taken"),
                    A(4, 7, DirectoryStatus.Questionable, -10, "partly refunded"),
                },
                new[] { L(7, DirectoryStatus.Questionable, "partly refunded") });

            var i = Assert.Single(issues);
            Assert.Equal(DirectoryStatus.Questionable, i.Status);
            Assert.Equal(Now.AddDays(-10), i.SinceUtc);
            Assert.Equal("partly refunded", i.Reason);
        }

        [Fact]
        public void OpenIssues_FallBackToTheCurrentNoteAndUpdateDate()
        {
            var noNote = OpenIssueBuilder.Build(
                new[] { A(1, 7, DirectoryStatus.Admitted, -50), A(2, 7, DirectoryStatus.Questionable, -10) },
                new[] { L(7, DirectoryStatus.Questionable, "Reseller on a blocklisted network") });
            Assert.Equal("Reseller on a blocklisted network", Assert.Single(noNote).Reason);

            var noAudit = OpenIssueBuilder.Build(
                Array.Empty<AuditStatusRow>(),
                new[] { L(8, DirectoryStatus.Scam, null, updated: Now.AddDays(-4)) });
            var i = Assert.Single(noAudit);
            Assert.Equal(Now.AddDays(-4), i.SinceUtc);
            Assert.Null(i.Reason);
        }

        [Fact]
        public void OpenIssues_CloseWhenTheListingIsBackToNormal_AndNeverIncludeRemovedListings()
        {
            var issues = OpenIssueBuilder.Build(
                new[]
                {
                    A(1, 7, DirectoryStatus.Scam, -30, "x"),
                    A(2, 7, DirectoryStatus.Verified, -5),
                    A(3, 8, DirectoryStatus.Scam, -10, "y"),
                    A(4, 8, DirectoryStatus.Removed, -3),
                    A(5, 9, DirectoryStatus.Questionable, -2, "newest"),
                    A(6, 10, DirectoryStatus.Scam, -8, "older"),
                },
                new[]
                {
                    L(7, DirectoryStatus.Verified, null),
                    L(8, DirectoryStatus.Removed, "y"),
                    L(9, DirectoryStatus.Questionable, "newest"),
                    L(10, DirectoryStatus.Scam, "older"),
                });

            Assert.Equal(new[] { 9, 10 }, issues.Select(i => i.DirectoryEntryId)); // newest first
        }

        [Fact]
        public void IncidentsJson_ListsOpenIssuesOnly_AndNeverLinksToScams()
        {
            var listings = new[]
            {
                L(7, DirectoryStatus.Scam, "took funds"),
                L(8, DirectoryStatus.Questionable, "slow refunds"),
                L(9, DirectoryStatus.Verified, null),
                L(10, DirectoryStatus.Removed, "gone"),
            };
            foreach (var l in listings)
            {
                l.Link = $"https://site{l.DirectoryEntryId}.example";
            }

            var issues = OpenIssueBuilder.Build(
                new[] { A(1, 7, DirectoryStatus.Scam, -20, "took funds"), A(2, 8, DirectoryStatus.Questionable, -3, "slow refunds") },
                listings);
            var json = OpenIssueJson.Build(issues, "https://monerica.com/", Now);

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var rows = doc.RootElement.GetProperty("openIssues").EnumerateArray().ToList();
            Assert.Equal(2, doc.RootElement.GetProperty("count").GetInt32());
            Assert.Equal(new[] { "listing-8", "listing-7" }, rows.Select(r => r.GetProperty("key").GetString()));

            var questionable = rows[0];
            Assert.Equal("Questionable", questionable.GetProperty("status").GetString());
            Assert.Equal("https://site8.example", questionable.GetProperty("website").GetString());
            Assert.Equal("https://monerica.com/site/listing-8", questionable.GetProperty("profileUrl").GetString());
            Assert.Equal("slow refunds", questionable.GetProperty("reason").GetString());
            Assert.Equal(Now.AddDays(-3), questionable.GetProperty("sinceUtc").GetDateTime().ToUniversalTime());

            var scam = rows[1];
            Assert.Equal("Scam", scam.GetProperty("status").GetString());
            Assert.False(scam.TryGetProperty("website", out _));
            Assert.DoesNotContain("site7.example", json);
            Assert.DoesNotContain("site9.example", json);
            Assert.DoesNotContain("gone", json);
        }

        [Fact]
        public void ProblemTags_AreWarningLevelPublicTags_NotMoneyBandsOrModeratorMarks()
        {
            Assert.True(ProblemTagRule.IsProblemTag(new ReviewTag { Slug = "funds-held", Level = ReviewTagLevel.Warning }));
            Assert.True(ProblemTagRule.IsProblemTag(new ReviewTag { Slug = "scam-report", Level = ReviewTagLevel.Scam }));
            Assert.False(ProblemTagRule.IsProblemTag(new ReviewTag { Slug = "valid-order", Level = ReviewTagLevel.Verified }));
            Assert.False(ProblemTagRule.IsProblemTag(new ReviewTag { Slug = "under-50", Level = ReviewTagLevel.Info, MaxUsd = 50 }));
            Assert.False(ProblemTagRule.IsProblemTag(new ReviewTag { Slug = "suspicious", Level = ReviewTagLevel.Warning, ReviewerSelectable = false }));
            Assert.False(ProblemTagRule.IsProblemTag(new ReviewTag { Slug = "old", Level = ReviewTagLevel.Risk, IsEnabled = false }));
            Assert.False(ProblemTagRule.IsProblemTag(null));
        }

        private static AuditStatusRow A(int id, int entryId, DirectoryStatus status, int daysAgo, string? note = null) => new ()
        {
            AuditId = id,
            DirectoryEntryId = entryId,
            Status = status,
            EffectiveUtc = Now.AddDays(daysAgo),
            Note = note,
        };

        private static IncidentListingRow L(int id, DirectoryStatus status, string? note, DateTime? updated = null) => new ()
        {
            CreateDate = Now.AddDays(-400),
            UpdateDate = updated,
            DirectoryEntryId = id,
            Name = $"Listing {id}",
            DirectoryEntryKey = $"listing-{id}",
            Status = status,
            Note = note,
        };

        private static DirectoryEntryViewModel Row(DateTime createDate) => new ()
        {
            DirectoryEntryId = 1,
            Name = "Fresh Shop",
            DirectoryEntryKey = "fresh-shop",
            Link = "https://fresh.example",
            DirectoryStatus = DirectoryStatus.Admitted,
            CreateDate = createDate,
            ItemPath = "/site/fresh-shop",
        };

        private static Dictionary<string, int> Cats() => new () { ["exchanges"] = 1, ["shops"] = 2 };

        private static Dictionary<string, int> Tags() => new () { ["funds-held"] = 15, ["valid-order"] = 1 };
    }
}
