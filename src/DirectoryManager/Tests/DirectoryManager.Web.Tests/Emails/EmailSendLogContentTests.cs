using System.Net;
using System.Reflection;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Models;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Tests.Emails
{
    // Stored message content for the admin email send log: dedupe, redaction, cap, failure
    // isolation, retention, the detail page model, filters and the sandboxed preview markup.
    public class EmailSendLogContentTests
    {
        private const string Secret = "test-sendgrid-key";

        [Fact]
        public async Task IdenticalContent_ToSeveralRecipients_IsStoredOnce()
        {
            using var h = new Harness();
            var service = h.NewEmailService();

            foreach (var to in new[] { "a@example.com", "b@example.com", "c@example.com" })
            {
                await service.SendEmailAsync("Issue 1", "Hello there", "<p>Hello there</p>", new List<string> { to });
            }

            using var db = h.NewDb();
            Assert.Equal(3, db.EmailSendLogs.Count());
            var content = Assert.Single(db.EmailSendLogContents.ToList());
            Assert.All(db.EmailSendLogs.ToList(), l => Assert.Equal(content.EmailSendLogContentId, l.EmailSendLogContentId));
            Assert.Equal("Hello there", content.BodyText);
            Assert.Equal("<p>Hello there</p>", content.BodyHtml);
            Assert.Equal("noreply@example.test", content.FromEmail);
            Assert.Equal("Test Sender", content.FromName);
        }

        [Fact]
        public async Task PerRecipientUnsubscribeHeaders_AreRedacted_AndStillDedupe()
        {
            using var h = new Harness();
            var service = h.NewEmailService(withUnsubscribe: true);
            var signer = new UnsubscribeTokenSigner(Secret);

            await service.SendEmailAsync("Weekly", "Body", "<p>Body</p>", new List<string> { "first@example.com" });
            await service.SendEmailAsync("Weekly", "Body", "<p>Body</p>", new List<string> { "second+tag@example.com" });

            using var db = h.NewDb();
            var content = Assert.Single(db.EmailSendLogContents.ToList());
            Assert.Contains("List-Unsubscribe:", content.Headers);
            Assert.Contains("token=[redacted]", content.Headers);
            Assert.Contains("email=[recipient]", content.Headers);
            Assert.DoesNotContain(signer.CreateToken("first@example.com")!, content.Headers);
            Assert.DoesNotContain("first", content.Headers);
            Assert.DoesNotContain("second", content.Headers);
            Assert.DoesNotContain(Secret, content.Headers);
        }

        [Fact]
        public async Task DifferentContent_IsStoredSeparately()
        {
            using var h = new Harness();
            var service = h.NewEmailService();

            await service.SendEmailAsync("S", "Body one", "<p>one</p>", new List<string> { "a@example.com" });
            await service.SendEmailAsync("S", "Body two", "<p>two</p>", new List<string> { "a@example.com" });
            await service.SendEmailAsync("S", "Body one", "<p>one</p>", new List<string> { "b@example.com" });

            using var db = h.NewDb();
            Assert.Equal(2, db.EmailSendLogContents.Count());
            Assert.Equal(3, db.EmailSendLogs.Count(l => l.EmailSendLogContentId != null));
        }

        [Fact]
        public async Task ContentStorageFailure_DoesNotFailTheSend_AndTheLogRowIsStillWritten()
        {
            using var h = new Harness(throwingContentRepo: true);
            var service = h.NewEmailService();

            await service.SendEmailAsync("Still sent", "x", "<p>x</p>", new List<string> { "a@example.com" });

            Assert.Equal(1, h.Handler.Requests);
            using var db = h.NewDb();
            var log = Assert.Single(db.EmailSendLogs.ToList());
            Assert.True(log.IsSuccess);
            Assert.Null(log.EmailSendLogContentId);
            Assert.Empty(db.EmailSendLogContents.ToList());
        }

        [Fact]
        public async Task FailedSend_StillStoresTheContent()
        {
            using var h = new Harness();
            h.Handler.Status = HttpStatusCode.BadRequest;
            var service = h.NewEmailService();

            await Assert.ThrowsAnyAsync<Exception>(() =>
                service.SendEmailAsync("Bad", "x", "<p>x</p>", new List<string> { "a@example.com" }));

            using var db = h.NewDb();
            var log = Assert.Single(db.EmailSendLogs.ToList());
            Assert.False(log.IsSuccess);
            Assert.NotNull(log.EmailSendLogContentId);
        }

        [Theory]
        [InlineData(
            "Confirm: https://app.monerica.com/newsletter/confirm?token=CfDJ8Abc-_x123Zz",
            "Confirm: https://app.monerica.com/newsletter/confirm?token=[redacted]")]
        [InlineData(
            "<https://app.monerica.com/unsubscribe/one-click?email=a%40example.com&token=0123456789abcdef0123456789abcdef>",
            "<https://app.monerica.com/unsubscribe/one-click?email=[recipient]&token=[redacted]>")]
        [InlineData(
            "<a href=\"https://app.monerica.com/unsubscribe/one-click?email=a%40example.com&amp;token=0123abcd\">x</a>",
            "<a href=\"https://app.monerica.com/unsubscribe/one-click?email=[recipient]&amp;token=[redacted]\">x</a>")]
        [InlineData(
            "Pay here: https://btcpay.example.test/i/AbCdEf123456 now",
            "Pay here: https://btcpay.example.test/i/[redacted] now")]
        [InlineData(
            "https://btcpay.example.test/invoice?id=AbCdEf123456",
            "https://btcpay.example.test/invoice?id=[redacted]")]
        [InlineData(
            "Reset: https://example.test/account/reset?user=5&code=XYZ789&next=/",
            "Reset: https://example.test/account/reset?user=5&code=[redacted]&next=/")]
        [InlineData(
            "Sign in: https://example.test/x?Password=hunter2 and https://example.test/y?sig=abc&key=def",
            "Sign in: https://example.test/x?Password=[redacted] and https://example.test/y?sig=[redacted]&key=[redacted]")]
        [InlineData(
            "Hi A@Example.com, you subscribed.",
            "Hi [recipient], you subscribed.")]
        [InlineData(
            "Listing: https://monerica.com/site/some-listing and https://app.monerica.com/site/abc/admin",
            "Listing: https://monerica.com/site/some-listing and https://app.monerica.com/site/abc/admin")]
        public void Redactor_ReplacesTheSecretsThisCodeBaseSends(string input, string expected)
        {
            Assert.Equal(expected, EmailContentRedactor.Redact(input, new[] { "a@example.com" }));
        }

        [Fact]
        public void RedactedConfirmationEmails_ForDifferentPeople_HashTheSame()
        {
            static EmailSendLogContent Confirm(string email, string token) => EmailContentSnapshot.Build(
                "noreply@example.test",
                "Test",
                null,
                null,
                $"Confirm your subscription for {email}: https://app.example.test/newsletter/confirm?token={token}",
                $"<p>Confirm {email}: <a href=\"https://app.example.test/newsletter/confirm?token={token}\">confirm</a></p>",
                new[] { email });

            var a = Confirm("one@example.com", "CfDJ8AAAA");
            var b = Confirm("two@example.com", "CfDJ8BBBB");

            Assert.Equal(a.ContentHash, b.ContentHash);
            Assert.DoesNotContain("CfDJ8AAAA", a.BodyText + a.BodyHtml);
            Assert.DoesNotContain("one@example.com", a.BodyText + a.BodyHtml);
            Assert.Equal(64, a.ContentHash.Length);
        }

        [Fact]
        public void LongBodies_AreCappedAndMarkedTruncated()
        {
            var big = new string('x', EmailContentSnapshot.MaxStoredBodyChars + 1000);

            var c = EmailContentSnapshot.Build("f@example.test", null, null, null, big, "<p>short</p>", new[] { "a@example.com" });

            Assert.True(c.BodyTextTruncated);
            Assert.Equal(EmailContentSnapshot.MaxStoredBodyChars, c.BodyText!.Length);
            Assert.False(c.BodyHtmlTruncated);
            Assert.Equal(big.Length + "<p>short</p>".Length, c.SizeBytes);

            // Same capped prefix but different tails must not collide (hash is over the full body).
            var other = EmailContentSnapshot.Build("f@example.test", null, null, null, big + "y", "<p>short</p>", new[] { "a@example.com" });
            Assert.NotEqual(c.ContentHash, other.ContentHash);
        }

        [Fact]
        public async Task Prune_DeletesOldContent_KeepsTheLogRows()
        {
            using var h = new Harness();
            using (var db = h.NewDb())
            {
                var old = new EmailSendLogContent { ContentHash = new string('a', 64), BodyText = "old", LastUsedUtc = DateTime.UtcNow.AddDays(-100) };
                var recent = new EmailSendLogContent { ContentHash = new string('b', 64), BodyText = "new", LastUsedUtc = DateTime.UtcNow.AddDays(-5) };
                db.EmailSendLogContents.AddRange(old, recent);
                db.SaveChanges();
                db.EmailSendLogs.AddRange(
                    new EmailSendLog { SourceApplication = "Web", RecipientEmail = "a@example.com", Subject = "old", EmailSendLogContentId = old.EmailSendLogContentId },
                    new EmailSendLog { SourceApplication = "Web", RecipientEmail = "a@example.com", Subject = "new", EmailSendLogContentId = recent.EmailSendLogContentId });
                db.SaveChanges();
            }

            using (var db = h.NewDb())
            {
                var removed = await new EmailSendLogContentRepository(db).PruneAsync(DateTime.UtcNow.AddDays(-90));
                Assert.Equal(1, removed);
            }

            using (var db = h.NewDb())
            {
                Assert.Equal("new", Assert.Single(db.EmailSendLogContents.ToList()).BodyText);
                Assert.Equal(2, db.EmailSendLogs.Count());
                Assert.Null(db.EmailSendLogs.Single(l => l.Subject == "old").EmailSendLogContentId);
                Assert.NotNull(db.EmailSendLogs.Single(l => l.Subject == "new").EmailSendLogContentId);
            }
        }

        [Fact]
        public async Task DetailPage_ShowsTheStoredContent_AndOtherSendsOfIt()
        {
            using var h = new Harness();
            var service = h.NewEmailService();
            await service.SendEmailAsync("Same", "t", "<p>h</p>", new List<string> { "a@example.com" });
            await service.SendEmailAsync("Same", "t", "<p>h</p>", new List<string> { "b@example.com" });

            using var db = h.NewDb();
            var id = db.EmailSendLogs.OrderBy(l => l.EmailSendLogId).First().EmailSendLogId;
            var view = Assert.IsType<ViewResult>(NewController(db).Details(id));
            var model = Assert.IsType<EmailSendLogDetailViewModel>(view.Model);

            Assert.NotNull(model.Content);
            Assert.Equal("<p>h</p>", model.Content!.BodyHtml);
            Assert.Equal(1, model.OtherSendsWithSameContent);
            Assert.Equal(90, model.RetentionDays);
        }

        [Fact]
        public void DetailPage_WithoutStoredContent_SaysSo_AndUnknownIdIs404()
        {
            using var h = new Harness();
            using var db = h.NewDb();
            db.EmailSendLogs.Add(new EmailSendLog { SourceApplication = "Web", RecipientEmail = "a@example.com", Subject = "Before", ErrorMessage = new string('e', 500) });
            db.SaveChanges();
            var id = db.EmailSendLogs.Single().EmailSendLogId;

            var view = Assert.IsType<ViewResult>(NewController(db).Details(id));
            var model = Assert.IsType<EmailSendLogDetailViewModel>(view.Model);
            Assert.Null(model.Content);
            Assert.Equal(500, model.Log.ErrorMessage!.Length);
            Assert.IsType<NotFoundResult>(NewController(db).Details(id + 1000));

            // The view's "not stored" sentence (Razor isn't rendered in unit tests, so check the template).
            var viewText = File.ReadAllText(FindViewFile("EmailSendLog", "Details.cshtml"));
            Assert.Contains("The message content was not stored", viewText);
            Assert.Contains("older than @Model.RetentionDays days", viewText);
            Assert.DoesNotContain("Html.Raw", viewText);
        }

        [Fact]
        public void ListFilters_Work()
        {
            using var h = new Harness();
            using var db = h.NewDb();
            db.EmailSendLogs.AddRange(
                new EmailSendLog { SourceApplication = "Web", RecipientEmail = "Alice@Example.com", Subject = "Welcome aboard", IsSuccess = true, EmailSendLogContentId = 7 },
                new EmailSendLog { SourceApplication = "Web", RecipientEmail = "bob@example.com", Subject = "Welcome aboard", IsSuccess = false },
                new EmailSendLog { SourceApplication = "NewsletterSender", RecipientEmail = "alice@example.com", Subject = "Issue 9", IsSuccess = true, EmailSendLogContentId = 7 });
            db.SaveChanges();
            var repo = new EmailSendLogRepository(db);

            Assert.Equal(2, repo.Search(new EmailSendLogFilter { RecipientContains = "ALICE" }, 0, 25, out var t1).Count);
            Assert.Equal(2, t1);
            Assert.Equal(2, repo.Search(new EmailSendLogFilter { SubjectContains = "welcome" }, 0, 25, out _).Count);
            Assert.Equal("bob@example.com", Assert.Single(repo.Search(new EmailSendLogFilter { IsSuccess = false }, 0, 25, out _)).RecipientEmail);
            Assert.Equal(2, repo.Search(new EmailSendLogFilter { ContentId = 7 }, 0, 25, out _).Count);
            Assert.Single(repo.Search(new EmailSendLogFilter { ContentId = 7, Source = "NewsletterSender" }, 0, 25, out _));
            Assert.Equal(3, repo.Search(new EmailSendLogFilter(), 0, 25, out _).Count);

            var view = Assert.IsType<ViewResult>(NewController(db).Index(result: "failed"));
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<EmailSendLog>>(view.Model));
        }

        [Theory]
        [InlineData(nameof(EmailSendLogController.Index))]
        [InlineData(nameof(EmailSendLogController.Details))]
        public void ListAndDetail_AreAdminOnly(string action)
        {
            var type = typeof(EmailSendLogController);
            var policies = type.GetCustomAttributes<AuthorizeAttribute>(true)
                .Concat(type.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>(true))
                .Select(a => a.Policy)
                .ToList();
            Assert.Contains(AuthorizationPolicies.AdminOnly, policies);
            Assert.Empty(type.GetMethod(action)!.GetCustomAttributes<AllowAnonymousAttribute>(true));
            Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>(true));
        }

        [Fact]
        public void PreviewIframe_KeepsHostileHtmlInsideTheEncodedAttribute()
        {
            const string hostile =
                "<p title=\"x\" onclick='go()'>\"quoted\" & 'single'</p></iframe><script>alert(1)</script>" +
                "<img src=\"https://tracker.example/p.gif\"><a href=\"javascript:alert(2)\">x</a>";

            var markup = EmailPreviewHelper.RenderIframe(hostile);

            Assert.StartsWith("<iframe ", markup);
            Assert.EndsWith("</iframe>", markup);
            Assert.Equal(1, Count(markup, "<iframe"));
            Assert.Equal(1, Count(markup, "</iframe>"));
            Assert.DoesNotContain("<script", markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<p", markup);
            Assert.DoesNotContain("<img", markup);
            Assert.Contains("sandbox=\"\"", markup);

            // Every quote inside the srcdoc value is encoded, so the attribute can't be closed early.
            var start = markup.IndexOf("srcdoc=\"", StringComparison.Ordinal) + "srcdoc=\"".Length;
            var end = markup.IndexOf('"', start);
            var value = markup.Substring(start, end - start);
            Assert.Equal(markup.Length - "</iframe>".Length, markup.IndexOf('<', end));
            Assert.Contains("&lt;script&gt;", value);
            Assert.Contains("&quot;quoted&quot;", value);
            Assert.Contains("img-src data:", WebUtility.HtmlDecode(value));

            // Decoded, the document starts with the blocking CSP and then the email as stored.
            var decoded = WebUtility.HtmlDecode(value);
            Assert.StartsWith("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none';", decoded);
            Assert.EndsWith(hostile, decoded);
        }

        [Fact]
        public void PreviewSrcdoc_PutsTheCspAfterALeadingDoctype()
        {
            var doc = EmailPreviewHelper.BuildSrcdoc("<!DOCTYPE html><html><body>x</body></html>");
            Assert.StartsWith("<!DOCTYPE html><meta http-equiv=\"Content-Security-Policy\"", doc);
        }

        private static int Count(string s, string what)
        {
            var n = 0;
            for (var i = s.IndexOf(what, StringComparison.OrdinalIgnoreCase); i >= 0; i = s.IndexOf(what, i + 1, StringComparison.OrdinalIgnoreCase))
            {
                n++;
            }

            return n;
        }

        private static string FindViewFile(string folder, string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "DirectoryManager.Web", "Views", folder, file);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new FileNotFoundException(file);
        }

        private static EmailSendLogController NewController(ApplicationDbContext db) =>
            new (new EmailSendLogRepository(db), new StaticOptions(new EmailSendLogOptions()));

        private sealed class StaticOptions : IOptionsMonitor<EmailSendLogOptions>
        {
            public StaticOptions(EmailSendLogOptions value) => this.CurrentValue = value;

            public EmailSendLogOptions CurrentValue { get; }

            public EmailSendLogOptions Get(string? name) => this.CurrentValue;

            public IDisposable? OnChange(Action<EmailSendLogOptions, string?> listener) => null;
        }

        private sealed class ThrowingContentRepository : IEmailSendLogContentRepository
        {
            public int GetOrCreate(EmailSendLogContent candidate) => throw new InvalidOperationException("db down");

            public EmailSendLogContent? Get(int emailSendLogContentId) => throw new InvalidOperationException("db down");

            public Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken ct = default) => throw new InvalidOperationException("db down");
        }

        private sealed class FakeSendGridHandler : HttpMessageHandler
        {
            public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;

            public int Requests { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                this.Requests++;
                return Task.FromResult(new HttpResponseMessage(this.Status) { Content = new StringContent(string.Empty) });
            }
        }

        private sealed class Harness : IDisposable
        {
            private readonly string dbName = $"email-content-{Guid.NewGuid():N}";
            private readonly ServiceProvider provider;

            public Harness(bool throwingContentRepo = false)
            {
                var services = new ServiceCollection();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(this.dbName));
                services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
                services.AddScoped<IEmailSendLogRepository, EmailSendLogRepository>();
                if (throwingContentRepo)
                {
                    services.AddScoped<IEmailSendLogContentRepository, ThrowingContentRepository>();
                }
                else
                {
                    services.AddScoped<IEmailSendLogContentRepository, EmailSendLogContentRepository>();
                }

                this.provider = services.BuildServiceProvider();
            }

            public FakeSendGridHandler Handler { get; } = new ();

            public EmailService NewEmailService(bool withUnsubscribe = false) => new (
                new SendGridConfig { ApiKey = Secret, SenderEmail = "noreply@example.test", SenderName = "Test Sender" },
                withUnsubscribe
                    ? new EmailSettings { UnsubscribeEmail = "unsubscribe@example.test", UnsubscribeUrlFormat = "https://app.example.test/unsubscribe?email={0}" }
                    : new EmailSettings(),
                this.provider.GetRequiredService<IServiceScopeFactory>(),
                "Web",
                new HttpClient(this.Handler));

            public ApplicationDbContext NewDb() => new (
                new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(this.dbName).Options);

            public void Dispose() => this.provider.Dispose();
        }
    }
}
