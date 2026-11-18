using System.Text;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Emails;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SendGrid.Helpers.Mail;

namespace DirectoryManager.Web.Tests.Emails
{
    // Newsletter double opt-in and RFC 8058 one-click unsubscribe. Real controller + real
    // repositories over an in-memory database; only sending email is faked.
    public class NewsletterDoubleOptInTests
    {
        private const string Secret = "test-sendgrid-key";

        [Fact]
        public async Task Subscribe_SendsOneConfirmationMail_AndCreatesNoSubscription()
        {
            using var h = new Harness();

            var result = Assert.IsType<ViewResult>(await h.SubscribeAsync("new@example.com"));
            Assert.Equal("CheckEmail", result.ViewName);

            Assert.Empty(h.Db.EmailSubscriptions);
            var mail = Assert.Single(h.Mail.Sent);
            Assert.Equal(new[] { "new@example.com" }, mail.Recipients);
            Assert.Contains("TestSite", mail.Subject);
            Assert.Contains("https://app.example.test/newsletter/confirm?token=", mail.Text);
            Assert.Contains("https://app.example.test/newsletter/confirm?token=", mail.Html);
        }

        [Fact]
        public async Task Subscribe_Twice_SendsOnlyOneMail_AndSamePage()
        {
            using var h = new Harness();

            var first = Assert.IsType<ViewResult>(await h.SubscribeAsync("new@example.com"));
            var second = Assert.IsType<ViewResult>(await h.SubscribeAsync("NEW@example.com"));

            Assert.Equal(first.ViewName, second.ViewName);
            Assert.Single(h.Mail.Sent);
        }

        [Fact]
        public async Task Subscribe_AlreadySubscribed_SendsNothing_AndSamePage()
        {
            using var h = new Harness();
            h.AddSubscription("member@example.com", subscribed: true);

            var result = Assert.IsType<ViewResult>(await h.SubscribeAsync("member@example.com"));

            Assert.Equal("CheckEmail", result.ViewName);
            Assert.Empty(h.Mail.Sent);
            Assert.True(h.Reload("member@example.com")!.IsSubscribed);
        }

        [Fact]
        public async Task Subscribe_WrongCaptcha_SendsNothing()
        {
            using var h = new Harness();

            var result = Assert.IsType<ViewResult>(await h.SubscribeAsync("new@example.com", solveCaptcha: false));

            Assert.NotEqual("CheckEmail", result.ViewName);
            Assert.Empty(h.Mail.Sent);
        }

        [Fact]
        public async Task ConfirmLink_Get_OnlyShowsButton_PostSubscribes()
        {
            using var h = new Harness();
            await h.SubscribeAsync("new@example.com");
            var token = h.TokenFromLastMail();

            var page = Assert.IsType<ViewResult>(h.Controller.Confirm(token));
            var model = Assert.IsType<NewsletterLinkModel>(page.Model);
            Assert.Equal("new@example.com", model.Email);
            Assert.Empty(h.Db.EmailSubscriptions); // a GET (link scanner) subscribes nobody

            var done = Assert.IsType<ViewResult>(await h.ConfirmPostAsync(token));
            Assert.Equal("ConfirmSubscribed", done.ViewName);
            var sub = Assert.Single(h.Db.EmailSubscriptions.AsNoTracking());
            Assert.Equal("new@example.com", sub.Email);
            Assert.True(sub.IsSubscribed);
        }

        [Fact]
        public async Task Confirm_IsIdempotent()
        {
            using var h = new Harness();
            await h.SubscribeAsync("new@example.com");
            var token = h.TokenFromLastMail();

            await h.ConfirmPostAsync(token);
            var again = Assert.IsType<ViewResult>(await h.ConfirmPostAsync(token));

            Assert.Equal("ConfirmSubscribed", again.ViewName);
            Assert.Single(h.Db.EmailSubscriptions.AsNoTracking());
            Assert.True(h.Reload("new@example.com")!.IsSubscribed);
        }

        [Fact]
        public async Task Confirm_ReEnablesAnUnsubscribedAddress()
        {
            using var h = new Harness();
            h.AddSubscription("back@example.com", subscribed: false);

            await h.SubscribeAsync("back@example.com");
            Assert.False(h.Reload("back@example.com")!.IsSubscribed); // not before confirming

            await h.ConfirmPostAsync(h.TokenFromLastMail());
            Assert.True(h.Reload("back@example.com")!.IsSubscribed);
            Assert.Single(h.Db.EmailSubscriptions.AsNoTracking());
        }

        [Theory]
        [InlineData("tampered")]
        [InlineData("expired")]
        [InlineData("garbage")]
        [InlineData("")]
        public async Task Confirm_RefusesBadTokens(string kind)
        {
            using var h = new Harness();
            await h.SubscribeAsync("new@example.com");
            var good = h.TokenFromLastMail();

            string token = kind switch
            {
                "tampered" => good[..^2] + (good.EndsWith("00", StringComparison.Ordinal) ? "11" : "00"),
                "expired" => h.MakeConfirmToken("new@example.com", DateTimeOffset.UtcNow.AddMinutes(-1)),
                "garbage" => "not-a-token--",
                _ => string.Empty,
            };

            var get = Assert.IsType<ViewResult>(h.Controller.Confirm(token));
            Assert.Null(get.Model);
            var post = Assert.IsType<ViewResult>(await h.ConfirmPostAsync(token));
            Assert.Equal("Confirm", post.ViewName);
            Assert.Null(post.Model);
            Assert.Empty(h.Db.EmailSubscriptions);
        }

        [Fact]
        public async Task OneClick_PostWithValidToken_Unsubscribes_ThatAddressOnly()
        {
            using var h = new Harness();
            h.AddSubscription("a@example.com", subscribed: true);
            h.AddSubscription("b@example.com", subscribed: true);
            var token = h.Signer.CreateToken("a@example.com");

            // What Gmail sends: a cookie-less POST with List-Unsubscribe=One-Click.
            var result = Assert.IsType<ViewResult>(h.OneClickPost("a@example.com", token));

            Assert.True(Assert.IsType<NewsletterLinkModel>(result.Model).Done);
            Assert.False(h.Reload("a@example.com")!.IsSubscribed);
            Assert.True(h.Reload("b@example.com")!.IsSubscribed);
        }

        [Fact]
        public void OneClick_GetShowsButton_AndChangesNothing()
        {
            using var h = new Harness();
            h.AddSubscription("a@example.com", subscribed: true);

            var result = Assert.IsType<ViewResult>(h.Controller.OneClickUnsubscribe("a@example.com", h.Signer.CreateToken("a@example.com")));

            Assert.False(Assert.IsType<NewsletterLinkModel>(result.Model).Done);
            Assert.True(h.Reload("a@example.com")!.IsSubscribed);
        }

        [Fact]
        public void OneClick_BadToken_DoesNothing_AndAnswersTheSameForMembersAndStrangers()
        {
            using var h = new Harness();
            h.AddSubscription("a@example.com", subscribed: true);

            var otherAddressToken = h.Signer.CreateToken("b@example.com");
            var member = Assert.IsType<ViewResult>(h.OneClickPost("a@example.com", otherAddressToken));
            var stranger = Assert.IsType<ViewResult>(h.OneClickPost("nobody@example.com", otherAddressToken));
            var empty = Assert.IsType<ViewResult>(h.OneClickPost("a@example.com", null));
            var wrongKey = Assert.IsType<ViewResult>(h.OneClickPost("a@example.com", new UnsubscribeTokenSigner("other-key").CreateToken("a@example.com")));

            foreach (var r in new[] { member, stranger, empty, wrongKey })
            {
                Assert.Equal("OneClickUnsubscribe", r.ViewName);
                Assert.Null(r.Model);
            }

            Assert.True(h.Reload("a@example.com")!.IsSubscribed);
        }

        [Fact]
        public void OneClick_ValidToken_SamePageWhetherOrNotSubscribed()
        {
            using var h = new Harness();

            var result = Assert.IsType<ViewResult>(h.OneClickPost("nobody@example.com", h.Signer.CreateToken("nobody@example.com")));

            Assert.True(Assert.IsType<NewsletterLinkModel>(result.Model).Done);
            Assert.Empty(h.Db.EmailSubscriptions);
        }

        [Fact]
        public void Signer_TokenIsCaseInsensitive_HexOnly_AndKeyed()
        {
            var signer = new UnsubscribeTokenSigner(Secret);
            var token = signer.CreateToken("Mixed@Example.com")!;

            Assert.Matches("^[0-9a-f]{32}$", token);
            Assert.True(signer.IsValid("mixed@example.com", token));
            Assert.True(signer.IsValid(" MIXED@EXAMPLE.COM ", token.ToUpperInvariant()));
            Assert.False(signer.IsValid("other@example.com", token));
            Assert.False(new UnsubscribeTokenSigner("different").IsValid("mixed@example.com", token));
            Assert.False(new UnsubscribeTokenSigner(null).IsValid("mixed@example.com", token));
            Assert.Null(new UnsubscribeTokenSigner(string.Empty).CreateToken("mixed@example.com"));
            Assert.Null(SuspiciousInputGuard.FindViolation(token));
        }

        [Fact]
        public void OneClickUrl_UsesConfiguredHost_AndCarriesTheToken()
        {
            var signer = new UnsubscribeTokenSigner(Secret);

            var url = EmailService.BuildOneClickUnsubscribeUrl("https://app.example.test/unsubscribe?email=a%2Bb%40example.com", "a+b@example.com", signer);

            Assert.Equal(
                $"https://app.example.test/unsubscribe/one-click?email=a%2Bb%40example.com&token={signer.CreateToken("a+b@example.com")}",
                url);
            Assert.Null(EmailService.BuildOneClickUnsubscribeUrl("http://app.example.test/unsubscribe", "a@example.com", signer));
            Assert.Null(EmailService.BuildOneClickUnsubscribeUrl("https://app.example.test/unsubscribe", "a@example.com", new UnsubscribeTokenSigner(null)));
        }

        [Fact]
        public void Headers_FollowRfc8058()
        {
            var service = new EmailService(
                new DirectoryManager.Services.Models.SendGridConfig { ApiKey = Secret, SenderEmail = "noreply@example.test", SenderName = "Test" },
                new DirectoryManager.Services.Models.EmailSettings { UnsubscribeEmail = "unsubscribe@example.test", UnsubscribeUrlFormat = "https://app.example.test/unsubscribe?email={0}" });
            var msg = new SendGridMessage();

            service.AddUnsubscribeHeaders("a@example.com", msg);

            // SendGrid's AddHeader stores the header on the message's (single) personalization.
            var headers = msg.Personalizations?.FirstOrDefault()?.Headers ?? msg.Headers;
            var token = new UnsubscribeTokenSigner(Secret).CreateToken("a@example.com");
            Assert.Equal(
                $"<mailto:unsubscribe@example.test>, <https://app.example.test/unsubscribe/one-click?email=a%40example.com&token={token}>",
                headers["List-Unsubscribe"]);
            Assert.Equal("List-Unsubscribe=One-Click", headers["List-Unsubscribe-Post"]);
        }

        private sealed class Harness : IDisposable
        {
            private readonly MemoryCache memoryCache = new (new MemoryCacheOptions());
            private readonly IDataProtectionProvider dataProtection = new EphemeralDataProtectionProvider();

            public Harness()
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"newsletter-optin-{Guid.NewGuid():N}")
                    .Options;
                this.Db = new ApplicationDbContext(options);

                this.Controller = new EmailSubscriptionController(
                    new EmailSubscriptionRepository(this.Db),
                    new EmailCampaignRepository(this.Db),
                    new EmailCampaignSubscriptionRepository(this.Db),
                    new EmailCampaignMessageRepository(this.Db),
                    new SentEmailRecordRepository(this.Db),
                    this.Mail,
                    new FakeSnippets(),
                    this.dataProtection,
                    this.memoryCache,
                    this.Signer,
                    NullLogger<EmailSubscriptionController>.Instance);
                this.NewRequest();
            }

            public ApplicationDbContext Db { get; }

            public FakeEmailService Mail { get; } = new ();

            public UnsubscribeTokenSigner Signer { get; } = new (Secret);

            public EmailSubscriptionController Controller { get; }

            public async Task<IActionResult> SubscribeAsync(string email, bool solveCaptcha = true)
            {
                this.NewRequest();
                CaptchaTools.Store(this.Controller.HttpContext, "newsletter", solveCaptcha ? "ABCDE" : "ZZZZZ");
                this.SetForm(new () { ["Captcha"] = "ABCDE", ["Email"] = email });
                return await this.Controller.Subscribe(new EmailSubscribeModel { Email = email, Captcha = "ABCDE" });
            }

            public async Task<IActionResult> ConfirmPostAsync(string token)
            {
                this.NewRequest();
                this.Db.ChangeTracker.Clear();
                return await this.Controller.ConfirmPost(token);
            }

            public IActionResult OneClickPost(string email, string? token)
            {
                this.NewRequest();
                this.Db.ChangeTracker.Clear();
                this.SetForm(new () { ["List-Unsubscribe"] = "One-Click" });
                return this.Controller.OneClickUnsubscribePost(email, token);
            }

            public string TokenFromLastMail()
            {
                var text = this.Mail.Sent[^1].Text;
                var start = text.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length;
                var end = text.IndexOfAny(new[] { '\n', ' ' }, start);
                return text[start..end];
            }

            public string MakeConfirmToken(string email, DateTimeOffset expires) =>
                Convert.ToHexStringLower(this.dataProtection
                    .CreateProtector(EmailSubscriptionController.ConfirmTokenPurpose)
                    .ToTimeLimitedDataProtector()
                    .Protect(Encoding.UTF8.GetBytes(email), expires));

            public void AddSubscription(string email, bool subscribed)
            {
                this.Db.EmailSubscriptions.Add(new EmailSubscription { Email = email, IsSubscribed = subscribed });
                this.Db.SaveChanges();
                this.Db.ChangeTracker.Clear();
            }

            public EmailSubscription? Reload(string email)
            {
                this.Db.ChangeTracker.Clear();
                return this.Db.EmailSubscriptions.AsNoTracking().FirstOrDefault(s => s.Email == email);
            }

            public void Dispose()
            {
                this.Db.Dispose();
                this.memoryCache.Dispose();
            }

            private void SetForm(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> values)
            {
                this.Controller.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
                this.Controller.HttpContext.Request.Form = new FormCollection(values);
            }

            private void NewRequest()
            {
                var http = new DefaultHttpContext { Session = new TestSession() };
                http.Request.Scheme = "https";
                http.Request.Host = new HostString("request.example.test");
                this.Controller.ControllerContext = new ControllerContext { HttpContext = http };
            }
        }

        private sealed class FakeEmailService : IEmailService
        {
            public List<(string Subject, string Text, string Html, List<string> Recipients)> Sent { get; } = new ();

            public Task SendEmailAsync(string subject, string plainTextContent, string htmlContent, List<string> recipients)
            {
                this.Sent.Add((subject, plainTextContent, htmlContent, recipients.ToList()));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSnippets : ICacheService
        {
            public void ClearSnippetCache(SiteConfigSetting snippetType)
            {
            }

            public Task<string> GetSnippetAsync(SiteConfigSetting snippetType) => Task.FromResult(snippetType switch
            {
                SiteConfigSetting.SiteName => "TestSite",
                SiteConfigSetting.AppDomain => "https://app.example.test/",
                SiteConfigSetting.CanonicalDomain => "https://example.test/",
                _ => string.Empty,
            });
        }

        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> store = new ();

            public bool IsAvailable => true;

            public string Id { get; } = Guid.NewGuid().ToString();

            public IEnumerable<string> Keys => this.store.Keys;

            public void Clear() => this.store.Clear();

            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Remove(string key) => this.store.Remove(key);

            public void Set(string key, byte[] value) => this.store[key] = value;

            public bool TryGetValue(string key, out byte[] value) => this.store.TryGetValue(key, out value!);
        }
    }
}
