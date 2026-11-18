using System.Reflection;
using System.Security.Claims;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Models.Submissions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryManager.Web.Tests.Submissions
{
    // The admin's quick Approve / Reject-with-reason on a single submission. Real controller and
    // repositories over an in-memory database; services the tested paths never touch are left out.
    public class SubmissionDecisionFlowTests
    {
        private const int EntryId = 10;
        private const int SubCategoryId = 2;

        [Fact]
        public async Task RejectPage_PreselectsTheReviewersReason()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync(s => s.ReviewPrefilterReason = "AutoReject: Same URL already submitted (submission #3)");

            var view = Assert.IsType<ViewResult>(await h.Controller.Reject(sub.SubmissionId, "ai-review"));
            Assert.Equal("Reject", view.ViewName);
            var vm = Assert.IsType<SubmissionRejectViewModel>(view.Model);
            Assert.Equal("duplicate", vm.Reason);
            Assert.Equal("ai-review", vm.From);
        }

        [Fact]
        public async Task Reject_WithListedReason_RecordsIt_AndReturnsToPendingList()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync();

            var result = await h.Controller.RejectPost(sub.SubmissionId, "no-monero", null, "pending");

            var redirect = Assert.IsType<LocalRedirectResult>(result);
            Assert.Equal("/submission/pending", redirect.Url);
            Assert.Contains("Rejected", h.Controller.TempData["SuccessMessage"] as string);

            var saved = await h.ReloadAsync(sub.SubmissionId);
            Assert.Equal(SubmissionStatus.Rejected, saved.SubmissionStatus);
            Assert.Equal(SubmissionReviewState.Rejected, saved.ReviewState);
            Assert.Contains("admin@example.com: rejected (Does not clearly show it accepts Monero", saved.ReviewLog);
            Assert.NotNull(saved.OriginalSnapshotJson);
        }

        [Fact]
        public async Task Reject_OtherWithText_FromAiReview_ReturnsThere()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync();

            var result = await h.Controller.RejectPost(sub.SubmissionId, "other", "Only sells gift cards for other coins", "ai-review");

            Assert.Equal("/admin/submission-review", Assert.IsType<LocalRedirectResult>(result).Url);
            Assert.NotNull(h.Controller.TempData["AiReviewMessage"]);
            var saved = await h.ReloadAsync(sub.SubmissionId);
            Assert.Equal(SubmissionStatus.Rejected, saved.SubmissionStatus);
            Assert.Contains("rejected (Only sells gift cards for other coins)", saved.ReviewLog);
        }

        [Fact]
        public async Task Reject_Spam_UsesSpamStatus_AndUnknownFromNeverRedirectsOffSite()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync();

            var result = await h.Controller.RejectPost(sub.SubmissionId, "spam", null, "https://evil.example/");

            Assert.Equal("/submission/pending", Assert.IsType<LocalRedirectResult>(result).Url);
            Assert.Equal(SubmissionStatus.Spam, (await h.ReloadAsync(sub.SubmissionId)).SubmissionStatus);
        }

        [Fact]
        public async Task Reject_OtherWithoutText_ShowsError_AndChangesNothing()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync();

            var view = Assert.IsType<ViewResult>(await h.Controller.RejectPost(sub.SubmissionId, "other", "  ", null));
            Assert.NotNull(Assert.IsType<SubmissionRejectViewModel>(view.Model).Error);

            var saved = await h.ReloadAsync(sub.SubmissionId);
            Assert.Equal(SubmissionStatus.Pending, saved.SubmissionStatus);
            Assert.Null(saved.ReviewLog);
        }

        [Fact]
        public async Task Reject_NotPending_ChangesNothing()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync(s => s.SubmissionStatus = SubmissionStatus.Approved);

            Assert.IsType<ViewResult>(await h.Controller.RejectPost(sub.SubmissionId, "duplicate", null, null));
            Assert.Equal(SubmissionStatus.Approved, (await h.ReloadAsync(sub.SubmissionId)).SubmissionStatus);
        }

        [Fact]
        public async Task QuickApprove_WithPgpKeySwap_IsStoppedUntilConfirmed()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync(s => s.DirectoryEntryId = EntryId);

            var posted = h.PostedEdit(sub, pgpKey: "ATTACKER KEY");
            var result = await h.ReviewPostAsync(sub.SubmissionId, posted, decision: "approve", confirmSensitive: false);

            var view = Assert.IsType<ViewResult>(result);
            Assert.False(h.Controller.ModelState.IsValid);
            Assert.Contains(h.Controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("PGP key"));

            // The page comes back still pending (not "Approved"), with nothing published.
            Assert.Equal(SubmissionStatus.Pending, Assert.IsType<Submission>(view.Model).SubmissionStatus);
            Assert.Equal(SubmissionStatus.Pending, (await h.ReloadAsync(sub.SubmissionId)).SubmissionStatus);
            var entry = await h.Db.DirectoryEntries.AsNoTracking().SingleAsync(e => e.DirectoryEntryId == EntryId);
            Assert.Equal("KEY-OWNER", entry.PgpKey);
        }

        [Fact]
        public async Task QuickApprove_WithoutSensitiveChanges_Approves_AndReturnsToPendingList()
        {
            using var h = new Harness();
            var sub = await h.AddSubmissionAsync(s => s.DirectoryEntryId = EntryId);

            var posted = h.PostedEdit(sub, description: "Instant swaps with no account");
            var result = await h.ReviewPostAsync(sub.SubmissionId, posted, decision: "approve", confirmSensitive: false, from: "pending");

            Assert.Equal("/submission/pending", Assert.IsType<LocalRedirectResult>(result).Url);
            Assert.Contains("Approved", h.Controller.TempData["SuccessMessage"] as string);
            var saved = await h.ReloadAsync(sub.SubmissionId);
            Assert.Equal(SubmissionStatus.Approved, saved.SubmissionStatus);
            Assert.Equal(SubmissionReviewState.Approved, saved.ReviewState);
            var entry = await h.Db.DirectoryEntries.AsNoTracking().SingleAsync(e => e.DirectoryEntryId == EntryId);
            Assert.Equal("Instant swaps with no account", entry.Description);
        }

        private sealed class Harness : IDisposable
        {
            public Harness()
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"submission-decision-{Guid.NewGuid()}")
                    .Options;
                this.Db = new ApplicationDbContext(options);
                this.Seed();

                var audit = new DirectoryEntriesAuditRepository(this.Db);
                this.Controller = new SubmissionController(
                    new UserManager<ApplicationUser>(NullProxy<IUserStore<ApplicationUser>>.Create(), null!, null!, null!, null!, null!, null!, null!, null!),
                    new SubmissionRepository(this.Db),
                    new SubcategoryRepository(this.Db),
                    new DirectoryEntryRepository(this.Db, audit),
                    audit,
                    new MemoryCache(new MemoryCacheOptions()),
                    null!, // cache service: only used by the public submit form
                    new TagRepository(this.Db),
                    new DirectoryEntryTagRepository(this.Db),
                    new AdditionalLinkRepository(this.Db),
                    new DirectoryEntryGuaranteeRepository(this.Db),
                    null!, // domain registration date: public submit form only
                    new ProcessorRepository(this.Db),
                    null!, // captcha: public submit form only
                    null!, // blocked terms: public submit form only
                    null!); // BTCPay: donation display only

                var http = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().BuildServiceProvider(),
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Name, "admin@example.com"), new Claim(ClaimTypes.NameIdentifier, "admin-1"), new Claim(ClaimTypes.Role, DirectoryManager.Data.Constants.AuthRoles.Admin) },
                        "test")),
                };
                this.Controller.ControllerContext = new ControllerContext { HttpContext = http };
                this.Controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
            }

            public ApplicationDbContext Db { get; }

            public SubmissionController Controller { get; }

            public async Task<Submission> AddSubmissionAsync(Action<Submission>? change = null)
            {
                var s = new Submission
                {
                    Name = "Example Swap",
                    Link = "https://example.com",
                    Description = "Instant swaps",
                    SubCategoryId = SubCategoryId,
                    SubmissionStatus = SubmissionStatus.Pending,
                    DirectoryStatus = DirectoryStatus.Admitted,
                };
                change?.Invoke(s);
                this.Db.Submissions.Add(s);
                await this.Db.SaveChangesAsync();
                this.Db.ChangeTracker.Clear();
                return s;
            }

            // The review form as posted: the listing's current values, plus the given edits.
            public Submission PostedEdit(Submission sub, string? pgpKey = "KEY-OWNER", string? description = null) => new Submission
            {
                Name = "Example Swap",
                Link = "https://example.com",
                Email = "owner@example.com",
                Description = description ?? "Instant swaps",
                PgpKey = pgpKey,
                SubCategoryId = SubCategoryId,
                DirectoryStatus = DirectoryStatus.Admitted,
                SubmissionStatus = SubmissionStatus.Pending, // the dropdown left as it was
                DirectoryEntryId = sub.DirectoryEntryId,
            };

            public Task<IActionResult> ReviewPostAsync(int id, Submission posted, string? decision, bool confirmSensitive, string? from = null) =>
                this.Controller.Review(
                    id,
                    posted,
                    Array.Empty<int>(),
                    new List<string>(),
                    new List<GuaranteeItem>(),
                    null,
                    null,
                    null,
                    CancellationToken.None,
                    decision,
                    confirmSensitive,
                    from);

            public async Task<Submission> ReloadAsync(int id)
            {
                this.Db.ChangeTracker.Clear();
                return await this.Db.Submissions.AsNoTracking().SingleAsync(s => s.SubmissionId == id);
            }

            public void Dispose() => this.Db.Dispose();

            private void Seed()
            {
                var category = new Category { CategoryId = 1, Name = "Exchanges", CategoryKey = "exchanges" };
                var subcat = new Subcategory { SubCategoryId = SubCategoryId, Name = "Instant Swaps", SubCategoryKey = "instant-swaps", Category = category, CategoryId = 1, IsEnabled = true };
                this.Db.Categories.Add(category);
                this.Db.Subcategories.Add(subcat);
                this.Db.DirectoryEntries.Add(new DirectoryEntry
                {
                    DirectoryEntryId = EntryId,
                    Name = "Example Swap",
                    DirectoryEntryKey = "example-swap",
                    Link = "https://example.com",
                    Email = "owner@example.com",
                    Description = "Instant swaps",
                    PgpKey = "KEY-OWNER",
                    SubCategory = subcat,
                    SubCategoryId = SubCategoryId,
                    DirectoryStatus = DirectoryStatus.Admitted,
                });
                this.Db.SaveChanges();
                this.Db.ChangeTracker.Clear();
            }
        }

        private sealed class NullTempDataProvider : ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }

        // Interface stub whose members are never called by these tests.
        private class NullProxy<T> : DispatchProxy
            where T : class
        {
            public static T Create() => Create<T, NullProxy<T>>();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
                throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
