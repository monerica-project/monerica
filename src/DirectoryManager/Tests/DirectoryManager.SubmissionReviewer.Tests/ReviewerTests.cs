using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class ReviewerTests
    {
        private static readonly List<ExistingListing> Listings = new ()
        {
            new (10, "Xeovo VPN", "https://xeovo.com/", null, null, false),
            new (11, "Old Shop", "https://oldshop.example/", null, null, true),
        };

        private static readonly (int, string)[] NoSubs = Array.Empty<(int, string)>();

        [Theory]
        [InlineData("https://www.Example.com/shop/?ref=1#x", "example.com/shop")]
        [InlineData("http://example.com/shop", "example.com/shop")]
        [InlineData("example.com", "example.com")]
        [InlineData("https://example.com/index.html", "example.com")]
        [InlineData("https://EXAMPLE.com/", "example.com")]
        public void Normalize_IgnoresSchemeWwwSlashQueryAndCase(string url, string expected) =>
            Assert.Equal(expected, UrlNormalizer.Normalize(url));

        [Fact]
        public void Normalize_KeepsDifferentPathsApart() =>
            Assert.NotEqual(UrlNormalizer.Normalize("https://a.com/x"), UrlNormalizer.Normalize("https://a.com/y"));

        [Fact]
        public void IsOnion_DetectsOnionHosts()
        {
            Assert.True(UrlNormalizer.IsOnion("http://abcdefghij234567.onion/shop"));
            Assert.False(UrlNormalizer.IsOnion("https://onion.example.com"));
        }

        [Fact]
        public void Prefilter_AlreadyListedUrl_IsAutoRejected()
        {
            var d = SubmissionPrefilter.Evaluate(Sub(link: "http://www.xeovo.com"), Listings, NoSubs, Ok());
            Assert.Equal(PrefilterOutcome.AutoReject, d.Outcome);
            Assert.Contains("Xeovo VPN", d.Reason);
        }

        [Fact]
        public void Prefilter_EditOfTheSameListing_IsNotADuplicate()
        {
            var s = Sub(link: "https://xeovo.com");
            s.DirectoryEntryId = 10;
            Assert.Equal(PrefilterOutcome.PassToAi, SubmissionPrefilter.Evaluate(s, Listings, NoSubs, Ok()).Outcome);
        }

        [Fact]
        public void Prefilter_SameUrlAsRemovedListing_IsFlaggedNotRejected() =>
            Assert.Equal(PrefilterOutcome.Flag, SubmissionPrefilter.Evaluate(Sub(link: "https://oldshop.example"), Listings, NoSubs, Ok()).Outcome);

        [Fact]
        public void Prefilter_SameUrlAsAnotherSubmission_IsAutoRejected()
        {
            var d = SubmissionPrefilter.Evaluate(Sub(), Listings, new[] { (99, "https://newshop.example/") }, Ok());
            Assert.Equal(PrefilterOutcome.AutoReject, d.Outcome);
            Assert.Contains("#99", d.Reason);
        }

        [Fact]
        public void Prefilter_Unreachable_IsAutoRejected()
        {
            var dead = new FetchResult("https://newshop.example", false, null, null, "Connection failed (HostNotFound)", null);
            var d = SubmissionPrefilter.Evaluate(Sub(), Listings, NoSubs, dead);
            Assert.Equal(PrefilterOutcome.AutoReject, d.Outcome);
            Assert.Contains("HostNotFound", d.Reason);
        }

        [Fact]
        public void Prefilter_BotBlockedSite_PassesToAi()
        {
            var blocked = new FetchResult("https://newshop.example", true, 403, "<html>challenge</html>", "HTTP 403", new Uri("https://newshop.example"));
            Assert.Equal(PrefilterOutcome.PassToAi, SubmissionPrefilter.Evaluate(Sub(), Listings, NoSubs, blocked).Outcome);
        }

        [Theory]
        [InlineData("Crypto casino with Monero deposits and fast withdrawals")]
        [InlineData("Online pharmacy shipping worldwide, pay with XMR")]
        [InlineData("Adult content site accepting Monero payments for videos")]
        public void Prefilter_BannedCategory_IsFlagged(string description) =>
            Assert.Equal(PrefilterOutcome.Flag, SubmissionPrefilter.Evaluate(Sub(description: description), Listings, NoSubs, Ok()).Outcome);

        [Fact]
        public void Prefilter_SimilarHarmlessWords_AreNotBanned() =>
            Assert.Null(SubmissionPrefilter.BannedCategory(Sub(description: "Book appointment time slots and pay in Monero (XMR) for haircuts")));

        [Theory]
        [InlineData("")]
        [InlineData("cheap")]
        [InlineData("$$$ 1234567 !!!! #### 8888")]
        [InlineData("aaaaaaaaaaaaaaaaaaaaaaaa great site")]
        [InlineData("Visit https://a.com and https://b.com and www.c.com for the best deals")]
        public void Prefilter_Spam_IsAutoRejected(string description) =>
            Assert.Equal(PrefilterOutcome.AutoReject, SubmissionPrefilter.Evaluate(Sub(description: description), Listings, NoSubs, Ok()).Outcome);

        [Fact]
        public void Dossier_IncludesTheWholeNoteToAdmin()
        {
            var s = Sub();
            s.NoteToAdmin = "I run this shop. Monero is on the checkout page under Pay with crypto. " + new string('x', 400) + " END-OF-NOTE";
            var text = SubmissionDossierBuilder.Build(s, null, Ok(), null, null, Listings);
            Assert.Contains("Note to admin (private, from the submitter)", text);
            Assert.Contains("END-OF-NOTE", text);
        }

        [Fact]
        public void Prefilter_SiteCheckerProposal_IsSkipped()
        {
            var s = Sub();
            s.NoteToAdmin = "(automated submission) site offline";
            Assert.Equal(PrefilterOutcome.Skip, SubmissionPrefilter.Evaluate(s, Listings, NoSubs, null).Outcome);
        }

        [Fact]
        public void Extract_DropsScriptsAndReadsTitleMetaAndPaymentLines()
        {
            var html = """
                <html><head><title>Shop &amp; Co</title>
                <meta content="Privacy gear shop" name="description"><script>var monero = 1;</script><style>.x{}</style></head>
                <body><nav>Home</nav><p>We sell hardware wallets.</p>
                <div>Accepted payments: Monero (XMR), Bitcoin</div><p>Free shipping</p></body></html>
                """;
            var t = HtmlText.Extract(html);
            Assert.Equal("Shop & Co", t.Title);
            Assert.Equal("Privacy gear shop", t.MetaDescription);
            Assert.Single(t.PaymentLines);
            Assert.Contains("Monero (XMR)", t.PaymentLines[0]);
            Assert.DoesNotContain("var monero", t.Body);
            Assert.Contains("We sell hardware wallets.", t.Body);
        }

        [Fact]
        public void Extract_CapsTheBody()
        {
            var t = HtmlText.Extract("<p>" + new string('a', 5000) + "</p>", maxBody: 3000);
            Assert.True(t.Body.Length <= 3001);
        }

        [Fact]
        public void FindPaymentLink_PrefersMoneroLinkOnTheSameSite()
        {
            var html = """
                <a href="https://other.com/monero">Other site Monero</a>
                <a href="/pricing">Pricing</a>
                <a href="/help/pay-with-xmr">Pay with XMR</a>
                """;
            Assert.Equal("https://shop.example/help/pay-with-xmr", HtmlText.FindPaymentLink(html, new Uri("https://shop.example/")));
        }

        [Fact]
        public void FindPaymentLink_FallsBackToPaymentPages() =>
            Assert.Equal("https://shop.example/pricing", HtmlText.FindPaymentLink("<a href='/about'>About</a><a href='/pricing'>Pricing</a>", new Uri("https://shop.example/")));

        [Fact]
        public void Dossier_StaysWithinTheLimit()
        {
            var page = new FetchResult("https://newshop.example", true, 200, "<p>" + string.Join(" ", Enumerable.Repeat("Monero accepted here.", 2000)) + "</p>", null, new Uri("https://newshop.example"));
            var text = SubmissionDossierBuilder.Build(Sub(), "Shopping > Electronics", page, page, null, Listings, 6000);
            Assert.True(text.Length <= 6000);
            Assert.Contains("NEW listing", text);
            Assert.Contains("Most similar existing listings", text);
        }

        [Fact]
        public void MostSimilar_FindsNameOrDomainLookalikes()
        {
            var s = Sub(name: "Xeovo", link: "https://xeovo.net");
            Assert.Equal(10, SubmissionDossierBuilder.MostSimilar(s, Listings)[0].Id);
        }

        [Fact]
        public void Verdict_ValidJson_Parses()
        {
            var v = VerdictParser.TryParse(ValidJson("approve_candidate"), out var err);
            Assert.Null(err);
            Assert.NotNull(v);
            Assert.Equal("approve_candidate", v!.Verdict);
            Assert.Equal("Tidy description.", v.Proposal.Description);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not json at all")]
        [InlineData("{\"verdict\": \"approve_candidate\", \"confidence\": ")]
        [InlineData("{\"verdict\": \"maybe\", \"confidence\": 0.5}")]
        public void Verdict_Malformed_ReturnsNullWithError(string json)
        {
            Assert.Null(VerdictParser.TryParse(json, out var err));
            Assert.False(string.IsNullOrEmpty(err));
        }

        [Fact]
        public void Verdict_CodeFence_IsTolerated() =>
            Assert.NotNull(VerdictParser.TryParse("```json\n" + ValidJson("flag") + "\n```", out _));

        [Fact]
        public void Verdict_TooLongDescription_IsDroppedWithIssue()
        {
            var v = VerdictParser.TryParse(ValidJson("approve_candidate", new string('x', 176)), out _);
            Assert.Null(v!.Proposal.Description);
            Assert.Contains(v.Issues, i => i.Contains("176"));
        }

        [Fact]
        public void Verdict_ConfidenceOutOfRange_IsZeroed()
        {
            var v = VerdictParser.TryParse(ValidJson("flag").Replace("0.9", "7"), out _);
            Assert.Equal(0, v!.Confidence);
        }

        [Fact]
        public void ParseResponse_ReadsTextAndTokens()
        {
            var r = ClaudeReviewService.ParseResponse("""{"model":"claude-sonnet-5-5","stop_reason":"end_turn","content":[{"type":"text","text":"{\"a\":1}"}],"usage":{"input_tokens":100,"cache_read_input_tokens":2000,"output_tokens":50}}""", "m");
            Assert.Null(r.Error);
            Assert.Equal("{\"a\":1}", r.Json);
            Assert.Equal(2100, r.InputTokens);
            Assert.Equal(50, r.OutputTokens);
            Assert.Equal("end_turn", r.StopReason);
        }

        [Theory]
        [InlineData("refusal", "declined")]
        [InlineData("max_tokens", "cut off")]
        public void ParseResponse_RefusalOrCutoff_IsAnError(string stop, string expected)
        {
            var r = ClaudeReviewService.ParseResponse($$$"""{"model":"m","stop_reason":"{{{stop}}}","content":[],"usage":{"input_tokens":1,"output_tokens":1}}""", "m");
            Assert.Contains(expected, r.Error);
        }

        [Fact]
        public void ParseResponse_Garbage_IsAnError() =>
            Assert.NotNull(ClaudeReviewService.ParseResponse("<html>bad gateway</html>", "m").Error);

        [Fact]
        public void Mapper_FillsProposedFieldsAndLeavesTheSubmissionAlone()
        {
            var s = Sub();
            var v = VerdictParser.TryParse(ValidJson("approve_candidate"), out _)!;
            ProposalMapper.Apply(s, v, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Shopping > Electronics"] = 7, ["Shopping > Gifts"] = 8 });

            Assert.Equal("Original description of the new shop.", s.Description);
            Assert.Equal("Tidy description.", s.ProposedDescription);
            Assert.Equal("NewShop", s.ProposedName);
            Assert.Equal(7, s.ProposedSubCategoryId);
            Assert.Equal("Shopping > Gifts", s.ProposedOtherSubcategories);
            Assert.Equal(KycPolicy.GuaranteedNoKyc, s.ProposedKycPolicy);
            Assert.Equal(SubmissionReviewState.AiApproveCandidate, s.ReviewState);
        }

        [Fact]
        public void Mapper_UnknownSubcategory_KeepsSubmittedOneWithIssue()
        {
            var s = Sub();
            s.SubCategoryId = 3;
            var v = VerdictParser.TryParse(ValidJson("approve_candidate"), out _)!;
            ProposalMapper.Apply(s, v, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            Assert.Equal(3, s.ProposedSubCategoryId);
            Assert.Contains(v.Issues, i => i.Contains("not in the list"));
        }

        [Fact]
        public void Mapper_BannedCategory_IsAlwaysFlagged()
        {
            var v = VerdictParser.TryParse(ValidJson("approve_candidate").Replace("\"banned_category\": false", "\"banned_category\": true"), out _)!;
            Assert.Equal(SubmissionReviewState.Flagged, ProposalMapper.StateFor(v));
        }

        [Fact]
        public void Schema_NeverPutsAnEnumOnATypeArray()
        {
            // The API rejects {"type": ["string","null"], "enum": [...]} (HTTP 400): nullable enums must use anyOf.
            void Walk(System.Text.Json.Nodes.JsonNode? n)
            {
                if (n is System.Text.Json.Nodes.JsonObject o)
                {
                    Assert.False(o.ContainsKey("enum") && o["type"] is System.Text.Json.Nodes.JsonArray, o.ToJsonString());
                    foreach (var kv in o)
                    {
                        Walk(kv.Value);
                    }
                }
                else if (n is System.Text.Json.Nodes.JsonArray a)
                {
                    foreach (var x in a)
                    {
                        Walk(x);
                    }
                }
            }

            Walk(ClaudeReviewService.Schema());
        }

        [Fact]
        public void SystemPrompt_CarriesTheSubmitPageRules()
        {
            var p = ClaudeReviewService.SystemPrompt(new ReviewContext(new[] { "Shopping > Gifts" }, new[] { "Open Source" }, new[] { "X — y" }));
            Assert.Contains("do NOT include the site's own name", p);
            Assert.Contains("do NOT say it accepts Monero", p);
            Assert.Contains("Mixers and tumblers", p);
            Assert.Contains("Shopping > Gifts", p);
            Assert.Contains("Open Source", p);
        }

        [Theory]
        [InlineData("Fast swaps!", "Fast swaps.")]
        [InlineData("Great!!! Easy to use!", "Great. Easy to use.")]
        [InlineData("Really?!", "Really?")]
        [InlineData("No exclamation here.", "No exclamation here.")]
        [InlineData("See example.com/#!/page for more", "See example.com/#!/page for more")]
        public void RemoveExclamations_NeverEndsWithBang(string input, string expected) =>
            Assert.Equal(expected, DirectoryManager.Data.Helpers.ListingText.RemoveExclamations(input));

        [Fact]
        public void Mapper_StripsExclamationsFromSuggestedText()
        {
            var s = Sub();
            var v = VerdictParser.TryParse(ValidJson("approve_candidate", "Fast private swaps!"), out _)!;
            ProposalMapper.Apply(s, v, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
            Assert.Equal("Fast private swaps.", s.ProposedDescription);
        }

        [Fact]
        public void Dossier_WarnsWhenProofLinkIsOnAnotherDomain()
        {
            var s = Sub();
            s.ProofLink = "https://xmrchain.net/tx/abc";
            Assert.Contains("NOT on the submitted site's domain", SubmissionDossierBuilder.Build(s, null, Ok(), null, null, Listings));
        }

        [Fact]
        public void SystemPrompt_HasAccountProofAndAdultRules()
        {
            var p = ClaudeReviewService.SystemPrompt(new ReviewContext(new[] { "Businesses > Pornography & Adult Products" }, Array.Empty<string>(), Array.Empty<string>()));
            Assert.Contains("after creating an account", p);
            Assert.Contains("Proof link", p);
            Assert.Contains("Businesses > Pornography & Adult Products", p);
            Assert.Contains("never an exclamation mark", p);
        }

        [Fact]
        public void Mapper_NullNote_DropsTheSubmittedNote()
        {
            var s = Sub();
            s.Note = "Launch promo: low rates this week!";
            ProposalMapper.Apply(s, VerdictParser.TryParse(ValidJson("flag"), out _)!, new Dictionary<string, int>());
            Assert.Null(s.ProposedNote);
            Assert.Equal("Launch promo: low rates this week!", s.Note);
        }

        private static FetchResult Ok() => new ("https://newshop.example", true, 200, "<p>ok</p>", null, new Uri("https://newshop.example"));

        private static Submission Sub(string name = "newshop", string link = "https://newshop.example", string description = "Original description of the new shop.") =>
            new () { SubmissionId = 1, Name = name, Link = link, Description = description, SubmissionStatus = SubmissionStatus.Pending };

        private static string ValidJson(string verdict, string description = "Tidy description.") => $$$"""
            {"verdict": "{{{verdict}}}", "confidence": 0.9, "monero_evidence": "checkout_or_instructions",
             "suggested_main_subcategory": "Shopping > Electronics", "suggested_subcategories": ["Shopping > Gifts", "Nope > Missing"],
             "duplicate_of": null, "banned_category": false, "issues": [], "summary": "Sells gear, XMR at checkout.",
             "proposal": {"name": "NewShop", "description": "{{{description}}}", "note": null, "country_code": "us", "location": null,
               "kyc_policy": "GuaranteedNoKyc", "liquidity": null, "rate_type": null, "tags": null,
               "changes": [{"field": "description", "reason": "clearer"}]}}
            """;
    }
}
