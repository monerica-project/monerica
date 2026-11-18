using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DirectoryManager.Data.Tests.Helpers
{
    public class ListingRestrictionsTests
    {
        // ---- country list: input → stored form ----

        [Theory]
        [InlineData("us", "US")]
        [InlineData("US, GB", "GB,US")]
        [InlineData("Iran, north korea, CU", "CU,IR,KP")]
        [InlineData("USA; UK", "GB,US")]
        [InlineData("United States, us, USA", "US")]
        [InlineData(" Cuba ,, Iran. ", "CU,IR")]
        [InlineData("the Netherlands", "NL")]
        [InlineData("US\r\nCA", "CA,US")]
        public void TryNormalizeCountries_AcceptsCodesNamesAndAliases_SortedAndDeduplicated(string input, string expected)
        {
            Assert.True(ListingRestrictions.TryNormalizeCountries(input, out var normalized, out var unknown));
            Assert.Empty(unknown);
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ,  ; ")]
        public void TryNormalizeCountries_BlankInput_IsNull(string? input)
        {
            Assert.True(ListingRestrictions.TryNormalizeCountries(input, out var normalized, out _));
            Assert.Null(normalized);
        }

        [Fact]
        public void TryNormalizeCountries_RejectsUnknownEntries_AndNamesThem()
        {
            Assert.False(ListingRestrictions.TryNormalizeCountries("US, Narnia, XX, EU", out var normalized, out var unknown));
            Assert.Equal(new[] { "Narnia", "XX", "EU" }, unknown);
            Assert.Equal("US", normalized);

            var error = ListingRestrictions.CountriesError("US, Narnia");
            Assert.NotNull(error);
            Assert.Contains("\"Narnia\"", error);
            Assert.Contains("two-letter", error);
            Assert.Null(ListingRestrictions.CountriesError("US, Iran"));
        }

        [Fact]
        public void EveryCountry_FitsTheColumn()
        {
            var all = string.Join(",", DirectoryManager.Utilities.Helpers.CountryHelper.GetCountries().Keys);
            Assert.True(ListingRestrictions.TryNormalizeCountries(all, out var normalized, out _));
            Assert.True(normalized!.Length <= ListingRestrictions.RestrictedCountriesMaxLength);
        }

        [Fact]
        public void InputText_RoundTripsThroughNames()
        {
            const string stored = "CU,IR,KP,US";
            var text = ListingRestrictions.ToInputText(stored);
            Assert.Equal("Cuba, Iran, North Korea, United States", text);
            Assert.Equal(stored, ListingRestrictions.NormalizeCountries(text));
            Assert.Equal("CU, IR, KP, US", ListingRestrictions.ToCodeText(stored));
            Assert.Null(ListingRestrictions.ToInputText(null));
        }

        [Fact]
        public void ParseCodes_IgnoresJunkInStoredValue() =>
            Assert.Equal(new[] { "US", "IR" }, ListingRestrictions.ParseCodes("us, IR,ZZ,US"));

        // ---- profile summary ----

        [Fact]
        public void Summary_NothingRecorded_IsEmpty()
        {
            var s = ListingRestrictions.Summarize(null, VpnPolicy.Unknown, TorPolicy.Unknown, "  ", hasOnionLink: false);
            Assert.True(s.IsEmpty);
            Assert.Equal(string.Empty, s.SummaryLine);
            Assert.False(s.NeedsDetails);
            Assert.Null(ListingRestrictions.FullText(null, VpnPolicy.Unknown, TorPolicy.Unknown, null));
        }

        [Fact]
        public void Summary_ManyCountriesAndNoVpn_CountsCountries_AndExpands()
        {
            var codes = "AF,BY,CU,IR,KP,MM,RU,SY,VE,YE,SD,SO,LY,IQ";
            var s = ListingRestrictions.Summarize(codes, VpnPolicy.NotAllowed, TorPolicy.Unknown, null, false);
            Assert.Equal("14 countries not served · VPN not allowed", s.SummaryLine);
            Assert.True(s.NeedsDetails);
            Assert.Equal(14, s.CountryNames.Count);
            Assert.Equal("Afghanistan", s.CountryNames[0]);
            Assert.Equal("Not allowed", s.Vpn);
        }

        [Theory]
        [InlineData("IR", "Not served in Iran")]
        [InlineData("US,IR", "Not served in Iran and United States")]
        public void Summary_OneOrTwoCountries_AreNamed_NoExpand(string codes, string expected)
        {
            var s = ListingRestrictions.Summarize(codes, VpnPolicy.Unknown, TorPolicy.Unknown, null, false);
            Assert.Equal(expected, s.SummaryLine);
            Assert.False(s.NeedsDetails);
        }

        [Fact]
        public void Summary_VpnAndTorOnly()
        {
            var s = ListingRestrictions.Summarize(null, VpnPolicy.MayTriggerVerification, TorPolicy.Allowed, null, false);
            Assert.Equal("VPN may trigger verification · Tor allowed", s.SummaryLine);
            Assert.False(s.NeedsDetails);
        }

        [Fact]
        public void Summary_TorNotAllowed_IsDroppedWhenTheListingHasAnOnionLink()
        {
            var withOnion = ListingRestrictions.Summarize(null, VpnPolicy.Unknown, TorPolicy.NotAllowed, null, hasOnionLink: true);
            Assert.True(withOnion.IsEmpty);

            var without = ListingRestrictions.Summarize(null, VpnPolicy.Unknown, TorPolicy.NotAllowed, null, hasOnionLink: false);
            Assert.Equal("Tor not allowed", without.SummaryLine);
        }

        [Fact]
        public void Summary_ShortOther_IsInline_LongOther_Expands()
        {
            var shortOne = ListingRestrictions.Summarize(null, VpnPolicy.NotAllowed, TorPolicy.Unknown, "Minimum age 18", false);
            Assert.Equal("VPN not allowed · Minimum age 18", shortOne.SummaryLine);
            Assert.False(shortOne.NeedsDetails);

            var longText = "No business accounts, and accounts are limited to one per person and household";
            var longOne = ListingRestrictions.Summarize("IR", VpnPolicy.Unknown, TorPolicy.Unknown, longText, false);
            Assert.Equal("Not served in Iran · other restrictions", longOne.SummaryLine);
            Assert.True(longOne.NeedsDetails);
            Assert.Equal(longText, longOne.Other);
        }

        [Fact]
        public void Summary_OtherOnly_IsCapitalizedAndSingleLine()
        {
            var s = ListingRestrictions.Summarize(null, VpnPolicy.Unknown, TorPolicy.Unknown, "minimum\r\nage 18", false);
            Assert.Equal("Minimum age 18", s.SummaryLine);
            Assert.Equal("minimum age 18", s.Other);
        }

        [Fact]
        public void FullText_ListsEverything()
        {
            Assert.Equal(
                "Not served in: Cuba, Iran; VPN: Not allowed; Tor: Allowed; Other: Minimum age 18",
                ListingRestrictions.FullText("IR,CU", VpnPolicy.NotAllowed, TorPolicy.Allowed, "Minimum age 18"));
        }

        [Fact]
        public void TorConflict_OnlyForNotAllowedWithOnionLink()
        {
            Assert.NotNull(ListingRestrictions.TorConflictError(TorPolicy.NotAllowed, "https://x.com", "http://abc.onion"));
            Assert.Null(ListingRestrictions.TorConflictError(TorPolicy.NotAllowed, "https://x.com", null));
            Assert.Null(ListingRestrictions.TorConflictError(TorPolicy.Allowed, "http://abc.onion"));
        }

        // ---- models ----

        [Fact]
        public void Equals_CoversEveryRestrictionField()
        {
            DirectoryEntry Make() => new () { DirectoryEntryId = 1, Name = "A", DirectoryEntryKey = "a", Link = "https://a.com" };

            Assert.True(Make().Equals(Make()));

            var a = Make();
            a.RestrictedCountries = "US";
            Assert.False(a.Equals(Make()));

            a = Make();
            a.VpnPolicy = VpnPolicy.NotAllowed;
            Assert.False(a.Equals(Make()));

            a = Make();
            a.TorPolicy = TorPolicy.Allowed;
            Assert.False(a.Equals(Make()));

            a = Make();
            a.OtherRestrictions = "Minimum age 18";
            Assert.False(a.Equals(Make()));
        }

        [Fact]
        public void SubmissionSnapshot_IncludesRestrictions()
        {
            var json = new Submission
            {
                RestrictedCountries = "IR,US",
                VpnPolicy = VpnPolicy.NotAllowed,
                TorPolicy = TorPolicy.MayTriggerVerification,
                OtherRestrictions = "Minimum age 18",
            }.CreateSnapshotJson();

            Assert.Contains("\"RestrictedCountries\":\"IR,US\"", json);
            Assert.Contains("\"VpnPolicy\":\"NotAllowed\"", json);
            Assert.Contains("\"TorPolicy\":\"MayTriggerVerification\"", json);
            Assert.Contains("\"OtherRestrictions\":\"Minimum age 18\"", json);
        }

        [Fact]
        public void SubmissionContentMapper_CarriesRestrictions()
        {
            var entry = new DirectoryEntry
            {
                Name = "A",
                DirectoryEntryKey = "a",
                RestrictedCountries = "IR",
                VpnPolicy = VpnPolicy.Allowed,
                TorPolicy = TorPolicy.NotAllowed,
                OtherRestrictions = "x",
            };
            var submission = new Submission();
            SubmissionContentMapper.CopyContentFields(entry, submission);

            Assert.Equal("IR", submission.RestrictedCountries);
            Assert.Equal(VpnPolicy.Allowed, submission.VpnPolicy);
            Assert.Equal(TorPolicy.NotAllowed, submission.TorPolicy);
            Assert.Equal("x", submission.OtherRestrictions);
        }

        [Fact]
        public async Task AuditSnapshot_IncludesRestrictions_OnCreateAndUpdate()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"restrictions-audit-{Guid.NewGuid():N}")
                .Options;

            var audits = new List<DirectoryEntriesAudit>();
            var auditRepo = new Mock<IDirectoryEntriesAuditRepository>();
            auditRepo.Setup(r => r.CreateAsync(It.IsAny<DirectoryEntriesAudit>()))
                .Callback<DirectoryEntriesAudit>(audits.Add)
                .Returns(Task.CompletedTask);

            using (var context = new ApplicationDbContext(options))
            {
                var category = new Category { CategoryId = 1, Name = "C", CategoryKey = "c" };
                context.Subcategories.Add(new Subcategory { SubCategoryId = 1, Name = "S", SubCategoryKey = "s", Category = category });
                await context.SaveChangesAsync();

                await new DirectoryEntryRepository(context, auditRepo.Object).CreateAsync(new DirectoryEntry
                {
                    DirectoryEntryId = 7,
                    Name = "A",
                    DirectoryEntryKey = "a",
                    Link = "https://a.com",
                    SubCategoryId = 1,
                    RestrictedCountries = "CU,IR",
                    VpnPolicy = VpnPolicy.NotAllowed,
                    TorPolicy = TorPolicy.Allowed,
                    OtherRestrictions = "Minimum age 18",
                });
            }

            using (var context = new ApplicationDbContext(options))
            {
                await new DirectoryEntryRepository(context, auditRepo.Object).UpdateAsync(new DirectoryEntry
                {
                    DirectoryEntryId = 7,
                    Name = "A",
                    DirectoryEntryKey = "a",
                    Link = "https://a.com",
                    SubCategoryId = 1,
                    RestrictedCountries = "US",
                    VpnPolicy = VpnPolicy.MayTriggerVerification,
                    TorPolicy = TorPolicy.NotAllowed,
                    OtherRestrictions = "No business accounts",
                });

                var saved = await context.DirectoryEntries.SingleAsync(e => e.DirectoryEntryId == 7);
                Assert.Equal("US", saved.RestrictedCountries);
                Assert.Equal(VpnPolicy.MayTriggerVerification, saved.VpnPolicy);
            }

            Assert.Equal(2, audits.Count);
            Assert.Equal("CU,IR", audits[0].RestrictedCountries);
            Assert.Equal(VpnPolicy.NotAllowed, audits[0].VpnPolicy);
            Assert.Equal(TorPolicy.Allowed, audits[0].TorPolicy);
            Assert.Equal("Minimum age 18", audits[0].OtherRestrictions);
            Assert.Equal("US", audits[1].RestrictedCountries);
            Assert.Equal(VpnPolicy.MayTriggerVerification, audits[1].VpnPolicy);
            Assert.Equal(TorPolicy.NotAllowed, audits[1].TorPolicy);
            Assert.Equal("No business accounts", audits[1].OtherRestrictions);
        }

        /// <summary>
        /// Guards the "every listing field is in the audit snapshot" rule: each restriction
        /// property on DirectoryEntry must also exist on DirectoryEntriesAudit.
        /// </summary>
        [Theory]
        [InlineData(nameof(DirectoryEntry.RestrictedCountries))]
        [InlineData(nameof(DirectoryEntry.VpnPolicy))]
        [InlineData(nameof(DirectoryEntry.TorPolicy))]
        [InlineData(nameof(DirectoryEntry.OtherRestrictions))]
        public void AuditAndSubmission_HaveTheField(string name)
        {
            Assert.NotNull(typeof(DirectoryEntriesAudit).GetProperty(name));
            Assert.NotNull(typeof(Submission).GetProperty(name));
        }
    }
}
