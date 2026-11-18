using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Tests.Security;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Web.Tests.Members
{
    public class MemberReviewServiceTests
    {
        private static MemberReviewInputModel Input(string? body = null, byte rating = 4, string? proof = null, string? details = null) => new ()
        {
            Rating = rating,
            Body = body ?? ReviewTestData.GoodBody,
            OrderProof = proof,
            OrderProofContext = details,
        };

        [Fact]
        public async Task NewMemberReview_IsPending_AndCarriesTheMemberIdentity()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var svc = ReviewTestData.Service(h.Db);

            var result = await svc.SaveReviewAsync(member, entry, Input());

            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Values));
            var review = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync();
            Assert.Equal(ReviewModerationStatus.Pending, review.ModerationStatus); // never auto-approved
            Assert.Equal(member.Id, review.AuthorUserId);
            Assert.Equal("quiet_otter_4821", review.AuthorUsername);
            Assert.Equal(MemberAuthor.AuthorKey(member.Id), review.AuthorFingerprint);
            Assert.Equal(AutoModerationResult.None, review.AutoModerationResult);
            Assert.False(review.IsOfficial);
        }

        [Fact]
        public async Task SwapFacts_AreKeptForExchanges_DroppedElsewhere_AndClearedOnEdit()
        {
            // Owner request 2026-11-18: optional processing time and confirmations on exchange reviews.
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var exchange = await ReviewTestData.AddEntryAsync(h.Db);
            (await h.Db.Categories.SingleAsync(c => c.CategoryId == exchange.SubCategoryId)).CategoryKey = "exchanges";
            var shop = await ReviewTestData.AddEntryAsync(h.Db); // category key "exchanges-<id>": not the Exchanges category
            await h.Db.SaveChangesAsync();
            var svc = ReviewTestData.Service(h.Db);
            Assert.True(await svc.AcceptsSwapFactsAsync(exchange));
            Assert.False(await svc.AcceptsSwapFactsAsync(shop));

            var input = Input();
            input.ProcessingMinutes = 25;
            input.Confirmations = 10;
            Assert.True((await svc.SaveReviewAsync(member, exchange, input)).Succeeded);
            var other = Input(ReviewTestData.GoodBody + " Ordered twice now and both were fine.");
            other.ProcessingMinutes = 25;
            other.Confirmations = 10;
            Assert.True((await svc.SaveReviewAsync(member, shop, other)).Succeeded);

            var onExchange = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync(r => r.DirectoryEntryId == exchange.DirectoryEntryId);
            Assert.Equal(25, onExchange.ProcessingMinutes);
            Assert.Equal(10, onExchange.Confirmations);
            var onShop = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync(r => r.DirectoryEntryId == shop.DirectoryEntryId);
            Assert.Null(onShop.ProcessingMinutes);
            Assert.Null(onShop.Confirmations);

            // Out-of-range values are dropped, and an edit that leaves a box empty clears it.
            var edit = Input(ReviewTestData.GoodBody + " Update: a second swap took longer.");
            edit.ProcessingMinutes = 999999;
            edit.Confirmations = null;
            Assert.True((await svc.SaveReviewAsync(member, exchange, edit)).Succeeded);
            var edited = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync(r => r.DirectoryEntryId == exchange.DirectoryEntryId);
            Assert.Null(edited.ProcessingMinutes);
            Assert.Null(edited.Confirmations);
        }

        [Fact]
        public async Task SecondSave_EditsTheSameReview_AndSendsItBackToModeration()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var svc = ReviewTestData.Service(h.Db);

            Assert.True((await svc.SaveReviewAsync(member, entry, Input())).Succeeded);
            var first = await h.Db.DirectoryEntryReviews.SingleAsync();
            first.ModerationStatus = ReviewModerationStatus.Approved; // a moderator approved it
            await h.Db.SaveChangesAsync();

            var edit = await svc.SaveReviewAsync(member, entry, Input(ReviewTestData.GoodBody + " Update: still good a week later.", rating: 3));

            Assert.True(edit.Succeeded, string.Join("; ", edit.Errors.Values));
            Assert.True(edit.WasEdit);
            Assert.True(edit.WasPublished);
            var only = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync();
            Assert.Equal(first.DirectoryEntryReviewId, only.DirectoryEntryReviewId);
            Assert.Equal(ReviewModerationStatus.Pending, only.ModerationStatus);
            Assert.Equal((byte)3, only.Rating);
        }

        [Fact]
        public async Task ClosedOrRemovedListingsAcceptNoMemberReviews()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var svc = ReviewTestData.Service(h.Db);

            var closed = await ReviewTestData.AddEntryAsync(h.Db, reviewsDisabled: true);
            var removed = await ReviewTestData.AddEntryAsync(h.Db, status: DirectoryStatus.Removed);

            Assert.True((await svc.SaveReviewAsync(member, closed, Input())).NotAllowed);
            Assert.True((await svc.SaveReviewAsync(member, removed, Input())).NotAllowed);
            Assert.True((await svc.SaveReviewAsync(member, null, Input())).NotAllowed);
            Assert.Empty(h.Db.DirectoryEntryReviews);
        }

        [Fact]
        public async Task VerificationListings_StillRequireProofAndDetails()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var svc = ReviewTestData.Service(h.Db);

            var needsProof = await ReviewTestData.AddEntryAsync(h.Db, requireVerification: true);
            var r1 = await svc.SaveReviewAsync(member, needsProof, Input());
            Assert.False(r1.Succeeded);
            Assert.True(r1.Errors.ContainsKey(nameof(MemberReviewInputModel.OrderProof)));

            var needsDetails = await ReviewTestData.AddEntryAsync(h.Db, requireDetails: true);
            var r2 = await svc.SaveReviewAsync(member, needsDetails, Input());
            Assert.False(r2.Succeeded);
            Assert.True(r2.Errors.ContainsKey(nameof(MemberReviewInputModel.OrderProofContext)));

            // A note with no wallet address in it is not enough any more: the destination address is required.
            var vague = await svc.SaveReviewAsync(member, needsDetails, Input(details: "receiving address 84abc..."));
            Assert.True(vague.Errors.ContainsKey(nameof(MemberReviewInputModel.OrderProofContext)));

            var ok = await svc.SaveReviewAsync(member, needsDetails, Input(details: "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"));
            Assert.True(ok.Succeeded, string.Join("; ", ok.Errors.Values));
            Assert.Empty(h.Db.DirectoryEntryReviews.Where(r => r.ModerationStatus == ReviewModerationStatus.Approved));
        }

        [Fact]
        public async Task OrderProofMustBeOnTheListingsOwnSite_AndIsNeverAutoApproved()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var svc = ReviewTestData.Service(h.Db);

            var elsewhere = await svc.SaveReviewAsync(member, entry, Input(proof: "https://other-site.example/orders/1"));
            Assert.True(elsewhere.Errors.ContainsKey(nameof(MemberReviewInputModel.OrderProof)));

            var notUrl = await svc.SaveReviewAsync(member, entry, Input(proof: "order 1234 please"));
            Assert.True(notUrl.Errors.ContainsKey(nameof(MemberReviewInputModel.OrderProof)));

            var host = new Uri(entry.Link).Host;
            var good = await svc.SaveReviewAsync(member, entry, Input(proof: $"https://{host}/orders/1"));
            Assert.True(good.Succeeded, string.Join("; ", good.Errors.Values));
            var review = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync();
            Assert.Equal($"https://{host}/orders/1", review.OrderUrl);
            Assert.Equal(ReviewModerationStatus.Pending, review.ModerationStatus);
        }

        [Fact]
        public async Task DuplicateTextAndMissingRatingAreRejected()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var other = await ReviewTestData.AddEntryAsync(h.Db);
            var svc = ReviewTestData.Service(h.Db);
            var pgp = ReviewTestData.PgpReview(other, ReviewModerationStatus.Approved);
            h.Db.DirectoryEntryReviews.Add(pgp);
            await h.Db.SaveChangesAsync();

            var dup = await svc.SaveReviewAsync(member, entry, Input(pgp.Body));
            Assert.True(dup.Errors.ContainsKey(nameof(MemberReviewInputModel.Body)));

            var noRating = await svc.SaveReviewAsync(member, entry, new MemberReviewInputModel { Body = ReviewTestData.GoodBody });
            Assert.True(noRating.Errors.ContainsKey(nameof(MemberReviewInputModel.Rating)));

            var tooShort = await svc.SaveReviewAsync(member, entry, Input("ok"));
            Assert.True(tooShort.Errors.ContainsKey(nameof(MemberReviewInputModel.Body)));
        }

        [Fact]
        public async Task PerAccountDailyLimitOnNewReviews()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var svc = ReviewTestData.Service(h.Db);

            for (var i = 0; i < MemberReviewService.MaxNewReviewsPerDay; i++)
            {
                var e = await ReviewTestData.AddEntryAsync(h.Db);
                var r = await svc.SaveReviewAsync(member, e, Input(ReviewTestData.GoodBody + " number " + i));
                Assert.True(r.Succeeded, string.Join("; ", r.Errors.Values));
            }

            var over = await svc.SaveReviewAsync(member, await ReviewTestData.AddEntryAsync(h.Db), Input(ReviewTestData.GoodBody + " one more"));
            Assert.False(over.Succeeded);
            Assert.True(over.Errors.ContainsKey(string.Empty));

            // Next day, still capped by the number of posts waiting for a moderator? 5 < 10, so allowed.
            var later = ReviewTestData.Service(h.Db, new MutableClock(DateTimeOffset.UtcNow.AddDays(2)));
            Assert.True((await later.SaveReviewAsync(member, await ReviewTestData.AddEntryAsync(h.Db), Input(ReviewTestData.GoodBody + " next day"))).Succeeded);
        }

        [Fact]
        public async Task DisabledMembersCantPost()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            await h.Members.SetDisabledAsync(member.Id, true, false);

            Assert.True((await ReviewTestData.Service(h.Db).SaveReviewAsync(member, entry, Input())).NotAllowed);
        }

        [Fact]
        public async Task RepliesOnlyOnApprovedReviews_AndArePending()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var approved = ReviewTestData.PgpReview(entry, ReviewModerationStatus.Approved);
            var pending = ReviewTestData.PgpReview(entry, ReviewModerationStatus.Pending);
            h.Db.DirectoryEntryReviews.AddRange(approved, pending);
            await h.Db.SaveChangesAsync();
            var svc = ReviewTestData.Service(h.Db);

            Assert.True((await svc.SaveReplyAsync(member, pending.DirectoryEntryReviewId, new MemberReplyInputModel { Body = "Thanks for sharing this." })).NotAllowed);

            var ok = await svc.SaveReplyAsync(member, approved.DirectoryEntryReviewId, new MemberReplyInputModel { Body = "Same experience here, thanks for sharing." });
            Assert.True(ok.Succeeded, string.Join("; ", ok.Errors.Values));
            var reply = await h.Db.DirectoryEntryReviewComments.AsNoTracking().SingleAsync();
            Assert.Equal(ReviewModerationStatus.Pending, reply.ModerationStatus);
            Assert.Equal(member.Id, reply.AuthorUserId);
            Assert.Equal("quiet_otter_4821", reply.AuthorUsername);
            Assert.Equal(MemberAuthor.AuthorKey(member.Id), reply.AuthorFingerprint);

            entry.ReviewsDisabled = true;
            await h.Db.SaveChangesAsync();
            Assert.True((await svc.SaveReplyAsync(member, approved.DirectoryEntryReviewId, new MemberReplyInputModel { Body = "Another reply that should be refused." })).NotAllowed);
        }

        [Fact]
        public async Task MembersSeeAndDeleteOnlyTheirOwnPosts()
        {
            using var h = new IdentityHarness();
            var alice = await h.CreateMemberAsync("alice_maple_1001");
            var bob = await h.CreateMemberAsync("bob_cedar_2002");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var svc = ReviewTestData.Service(h.Db);

            Assert.True((await svc.SaveReviewAsync(alice, entry, Input(ReviewTestData.GoodBody + " alice"))).Succeeded);
            Assert.True((await svc.SaveReviewAsync(bob, entry, Input(ReviewTestData.GoodBody + " bob"))).Succeeded);
            var aliceReview = await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync(r => r.AuthorUserId == alice.Id);

            Assert.Single((await svc.ListOwnAsync(alice)).Reviews);
            Assert.Null(await svc.DeleteOwnReviewAsync(bob.Id, aliceReview.DirectoryEntryReviewId));
            Assert.Equal(entry.DirectoryEntryId, await svc.DeleteOwnReviewAsync(alice.Id, aliceReview.DirectoryEntryReviewId));
            Assert.Empty((await svc.ListOwnAsync(alice)).Reviews);
            Assert.Single((await svc.ListOwnAsync(bob)).Reviews);
        }

        [Fact]
        public async Task MemberReviewsNeverLookLikeTheOwnerOrModerator()
        {
            using var h = new IdentityHarness();
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var review = ReviewTestData.MemberReview(member, entry, ReviewModerationStatus.Approved);
            review.DirectoryEntry = entry;

            // Even if the listing's "PGP key" text were junk that yields short fingerprints, the
            // member key normalizes to nothing and cannot match.
            entry.PgpKey = "not a real key";
            ReviewAuthorFlags.ApplyOwnerFlags(new[] { review });
            Assert.False(review.IsOwner);
            Assert.Null(review.DisplayName);
        }
    }
}
