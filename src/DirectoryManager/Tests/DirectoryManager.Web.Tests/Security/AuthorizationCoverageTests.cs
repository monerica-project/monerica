using System.Reflection;
using DirectoryManager.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Tests.Security
{
    /// <summary>
    /// Every controller action needs a login unless it is on the list below. A new action that
    /// forgets [Authorize] fails this test, so a page can only become public on purpose.
    /// </summary>
    public class AuthorizationCoverageTests
    {
        // Controller => actions that are public by design ("*" = the whole controller).
        private static readonly Dictionary<string, string[]> PublicByDesign = new ()
        {
            ["Account"] = new[] { "Login" },
            ["Advertisers"] = new[] { "*" },
            ["Captcha"] = new[] { "*" },
            ["Category"] = new[] { "Category" },
            ["Countries"] = new[] { "*" },
            ["DirectoryEntry"] = new[] { "DirectoryEntryView" },
            ["DirectoryEntryReviewReplies"] = new[] { "*" },
            ["DirectoryEntryReviews"] = new[] { "BeginGet", "Begin", "Captcha", "CaptchaPost", "SubmitKey", "SubmitKeyPost", "VerifyCode", "VerifyCodePost", "Compose", "ComposePost", "Thanks" },
            ["DirectoryFilter"] = new[] { "*" },
            ["DonationWebhook"] = new[] { "*" },
            ["EmailSubscription"] = new[] { "*" },
            ["Errors"] = new[] { "*" },
            ["Favicon"] = new[] { "*" },
            ["Home"] = new[] { "IndexAsync", "ContactAsync", "FAQAsync", "DonateAsync", "AboutAsync", "TermsAsync", "NetworkAsync", "PgpAsync", "Newest" },
            ["HowWeVerify"] = new[] { "*" },
            ["Incidents"] = new[] { "*" },
            ["ListingHistory"] = new[] { "*" },
            ["Llms"] = new[] { "*" },
            ["MarketingOptOut"] = new[] { "*" },
            ["MemberAccount"] = new[] { "Register", "RegisterPost", "SignIn", "SignInPost" }, // sign-up / sign-in forms (captcha)
            ["MyReviews"] = new[] { "*" },             // gated by the reviewer's PGP session
            ["ProblemReports"] = new[] { "*" },
            ["PublicReviews"] = new[] { "*" },
            ["Raffle"] = new[] { "*" },
            ["Robots"] = new[] { "*" },
            ["Rss"] = new[] { "*" },
            ["Search"] = new[] { "*" },
            ["SiteMap"] = new[] { "*" },
            ["SiteOwnerAdmin"] = new[] { "*" },        // gated by the listing owner's PGP session
            ["Snippets"] = new[] { "*" },
            ["SponsorInterestBeacon"] = new[] { "*" },
            ["SponsoredListingCheckout"] = new[] { "*" },
            ["SponsoredListing"] = new[] { "IndexAsync", "Offers", "Current", "ActiveListings", "ActiveSponsorJsonAsync" },
            ["SponsoredListingNotification"] = new[] { "SubscribeAsync" },
            ["SponsorsAndFeatured"] = new[] { "*" },
            ["Sponsorship"] = new[] { "*" },
            ["StaticPublishInternal"] = new[] { "*" }, // loopback + shared secret (InternalEndpointGuard)
            ["SubCategory"] = new[] { "SubCategoryListings" },
            ["Submission"] = new[] { "CreateAsync", "Create", "Preview", "SubmitEdit", "FindExisting", "Success", "Pay", "PayPost", "PayInvoice", "FindingListing", "ConfirmAsync" },
            ["Tag"] = new[] { "*" },
            ["VerificationRequests"] = new[] { "*" },
            ["VideoSponsorship"] = new[] { "*" },
        };

        [Fact]
        public void EveryActionNeedsALogin_UnlessListedAsPublic()
        {
            var unexpected = new List<string>();

            var controllers = typeof(HomeController).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)
                            && t.GetCustomAttribute<NonControllerAttribute>() == null);

            foreach (var c in controllers)
            {
                var name = c.Name.EndsWith("Controller", StringComparison.Ordinal) ? c.Name[..^"Controller".Length] : c.Name;
                bool classAuthorize = c.GetCustomAttributes<AuthorizeAttribute>(true).Any();
                PublicByDesign.TryGetValue(name, out var allowed);
                allowed ??= Array.Empty<string>();

                var actions = c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

                foreach (var m in actions)
                {
                    bool anonymous = m.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()
                                     || (c.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() && !m.GetCustomAttributes<AuthorizeAttribute>(true).Any());
                    bool needsLogin = !anonymous && (classAuthorize || m.GetCustomAttributes<AuthorizeAttribute>(true).Any());
                    if (needsLogin)
                    {
                        continue;
                    }

                    if (!allowed.Contains("*") && !allowed.Contains(m.Name))
                    {
                        unexpected.Add($"{name}.{m.Name}");
                    }
                }
            }

            Assert.True(
                unexpected.Count == 0,
                "These actions can be reached without logging in but are not on the public list:\n" + string.Join("\n", unexpected.Distinct().OrderBy(x => x)));
        }
    }
}
