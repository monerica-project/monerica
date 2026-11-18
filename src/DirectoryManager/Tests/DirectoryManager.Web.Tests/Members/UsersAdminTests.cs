using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Web.Controllers;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using DirectoryManager.Web.Tests.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryManager.Web.Tests.Members
{
    /// <summary>
    /// The admin "Users" page: account and PGP-key lists (paging, search, sort, badges), detail
    /// pages, and posting blocks for accounts and keys across every way of posting.
    /// </summary>
    public class UsersAdminTests
    {
        private static readonly Lazy<MemberPgpTestKeys.TestKey> KeyA = new (() => MemberPgpTestKeys.Generate("A <a@example.com>"));
        private static readonly Lazy<MemberPgpTestKeys.TestKey> KeyB = new (() => MemberPgpTestKeys.Generate("B <b@example.com>"));
        private static readonly Lazy<MemberPgpTestKeys.TestKey> ModKey = new (() => MemberPgpTestKeys.Generate("Mod <mod@example.com>"));

        private static int nextFp = 1;

        // ------------------------------------------------------------------ lists

        [Fact]
        public async Task Accounts_PagingSearchSortAndStatusFilter()
        {
            using var h = new IdentityHarness();
            await h.EnsureAdminRoleAsync();
            var admin = await h.CreateUserAsync("owner@example.com");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);

            // 55 quiet accounts (added directly: the sign-up throttle is not what is tested here).
            for (int i = 0; i < 55; i++)
            {
                AddUser(h.Db, $"filler_{i:D2}", DateTime.UtcNow.AddDays(-200 + i));
            }

            var busy = AddUser(h.Db, "busy_bee", DateTime.UtcNow.AddDays(-300));
            var recent = AddUser(h.Db, "recent_poster", DateTime.UtcNow.AddDays(-250));
            var blocked = AddUser(h.Db, "blocked_one", DateTime.UtcNow.AddDays(-240));
            blocked.PostingBlockedUtc = DateTime.UtcNow;
            var gone = AddUser(h.Db, "gone_member", DateTime.UtcNow.AddDays(-230));
            gone.DeletedUtc = DateTime.UtcNow;
            busy.PgpFingerprint = KeyA.Value.Fingerprint;
            await h.Db.SaveChangesAsync();

            for (int i = 0; i < 3; i++)
            {
                var r = ReviewTestData.MemberReview(busy, entry, i == 0 ? ReviewModerationStatus.Approved : ReviewModerationStatus.Rejected);
                r.CreateDate = DateTime.UtcNow.AddDays(-50 - i);
                h.Db.DirectoryEntryReviews.Add(r);
            }

            var latest = ReviewTestData.MemberReview(recent, entry, ReviewModerationStatus.Pending);
            latest.CreateDate = DateTime.UtcNow.AddHours(-1);
            h.Db.DirectoryEntryReviews.Add(latest);
            await h.Db.SaveChangesAsync();

            var svc = Service(h);

            // Default: newest first, 50 per page, admins and deleted accounts not listed.
            var p1 = await svc.ListAsync(new UsersAdminQuery());
            Assert.Equal(58, p1.Total); // 55 + busy + recent + blocked
            Assert.Equal(2, p1.TotalPages);
            Assert.Equal(UsersAdminService.PageSize, p1.Accounts.Count);
            Assert.DoesNotContain(p1.Accounts, a => a.Id == admin.Id || a.Id == gone.Id);
            Assert.Equal("filler_54", p1.Accounts[0].Username);
            var p2 = await svc.ListAsync(new UsersAdminQuery { Page = 2 });
            Assert.Equal(8, p2.Accounts.Count);
            Assert.Empty(p1.Accounts.Select(a => a.Id).Intersect(p2.Accounts.Select(a => a.Id)));

            // Out-of-range page is clamped.
            Assert.Equal(2, (await svc.ListAsync(new UsersAdminQuery { Page = 99 })).Page);

            // Most posts / last active.
            var most = await svc.ListAsync(new UsersAdminQuery { Sort = UsersAdminSorts.MostPosts });
            Assert.Equal("busy_bee", most.Accounts[0].Username);
            Assert.Equal(1, most.Accounts[0].Reviews.Published);
            Assert.Equal(2, most.Accounts[0].Reviews.Rejected);
            var active = await svc.ListAsync(new UsersAdminQuery { Sort = UsersAdminSorts.LastActive });
            Assert.Equal("recent_poster", active.Accounts[0].Username);
            Assert.Equal("busy_bee", active.Accounts[1].Username);
            Assert.Equal(1, active.Accounts[0].Reviews.Waiting);
            Assert.NotNull(active.Accounts[0].LastPostUtc);

            // Username search (case-insensitive contains) and linked-key search.
            var byName = await svc.ListAsync(new UsersAdminQuery { Q = "BEE" });
            Assert.Equal("busy_bee", Assert.Single(byName.Accounts).Username);
            var keyId = KeyA.Value.Fingerprint[^16..].ToLowerInvariant();
            Assert.Equal("busy_bee", Assert.Single((await svc.ListAsync(new UsersAdminQuery { Key = keyId, Match = UsersAdminMatch.Suffix })).Accounts).Username);
            Assert.Equal("busy_bee", Assert.Single((await svc.ListAsync(new UsersAdminQuery { Key = KeyA.Value.Fingerprint[..10], Match = UsersAdminMatch.Prefix })).Accounts).Username);
            Assert.Empty((await svc.ListAsync(new UsersAdminQuery { Key = keyId, Match = UsersAdminMatch.Prefix })).Accounts);

            // Status filters.
            Assert.Equal("blocked_one", Assert.Single((await svc.ListAsync(new UsersAdminQuery { Status = UsersAdminStatuses.Blocked })).Accounts).Username);
            Assert.Equal("gone_member", Assert.Single((await svc.ListAsync(new UsersAdminQuery { Status = UsersAdminStatuses.Deleted })).Accounts).Username);
            Assert.Equal(57, (await svc.ListAsync(new UsersAdminQuery { Status = UsersAdminStatuses.Active })).Total);
            Assert.Equal(59, (await svc.ListAsync(new UsersAdminQuery { Status = UsersAdminStatuses.All })).Total);
        }

        [Fact]
        public async Task Keys_ExcludeMemberPosts_PagingSearchSort()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var entry2 = await ReviewTestData.AddEntryAsync(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            // A member review + reply: their synthetic usr: key must never be listed.
            h.Db.DirectoryEntryReviews.Add(ReviewTestData.MemberReview(member, entry, ReviewModerationStatus.Approved));

            // 52 one-post keys.
            var fps = new List<string>();
            for (int i = 0; i < 52; i++)
            {
                var fp = NewFingerprint();
                fps.Add(fp);
                h.Db.DirectoryEntryReviews.Add(PgpReview(entry, fp, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-100 + i)));
            }

            // One busy key: 2 reviews on two listings + a reply, first seen long ago, last seen now.
            var busy = NewFingerprint();
            h.Db.DirectoryEntryReviews.Add(PgpReview(entry, busy, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-400)));
            var busyReview2 = PgpReview(entry2, busy, ReviewModerationStatus.Pending, DateTime.UtcNow.AddDays(-300));
            h.Db.DirectoryEntryReviews.Add(busyReview2);
            await h.Db.SaveChangesAsync();
            h.Db.DirectoryEntryReviewComments.Add(PgpReply(busyReview2, busy, ReviewModerationStatus.Rejected, DateTime.UtcNow.AddMinutes(-5)));
            h.Db.DirectoryEntryReviewComments.Add(new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = busyReview2.DirectoryEntryReviewId,
                Body = "member reply",
                AuthorFingerprint = MemberAuthor.AuthorKey(member.Id),
                AuthorUserId = member.Id,
                ModerationStatus = ReviewModerationStatus.Approved,
                CreateDate = DateTime.UtcNow,
            });

            // A usr: row whose account link was lost (AuthorUserId null) is still not a PGP key.
            h.Db.DirectoryEntryReviews.Add(new DirectoryEntryReview
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                AuthorFingerprint = MemberAuthor.AuthorKey(Guid.NewGuid().ToString()),
                Body = "orphan member review",
                ModerationStatus = ReviewModerationStatus.Approved,
                CreateDate = DateTime.UtcNow,
            });
            await h.Db.SaveChangesAsync();

            var svc = Service(h);
            var p1 = await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys });
            Assert.Equal(53, p1.Total);
            Assert.Equal(2, p1.TotalPages);
            Assert.Equal(50, p1.Keys.Count);
            var p2 = await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Page = 2 });
            Assert.Equal(3, p2.Keys.Count);
            Assert.All(p1.Keys.Concat(p2.Keys), k => Assert.DoesNotContain(MemberAuthor.KeyPrefix, k.Fingerprint));

            // Newest (first seen) first: the busy key was first seen earliest, so it is last.
            Assert.Equal(busy, p2.Keys[^1].Fingerprint);

            var most = await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Sort = UsersAdminSorts.MostPosts });
            var top = most.Keys[0];
            Assert.Equal(busy, top.Fingerprint);
            Assert.Equal(1, top.Reviews.Published);
            Assert.Equal(1, top.Reviews.Waiting);
            Assert.Equal(1, top.Replies.Rejected);
            Assert.Equal(0, top.Replies.Published); // the member's reply on the same review is not counted
            Assert.Equal(2, top.ListingCount);
            Assert.Equal(DateTime.UtcNow.AddDays(-400).Date, top.FirstSeenUtc!.Value.Date);

            var active = await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Sort = UsersAdminSorts.LastActive });
            Assert.Equal(busy, active.Keys[0].Fingerprint);

            // Search: contains / prefix / suffix on the normalized hex (lower case and spaces are fine).
            var mid = busy.Substring(10, 12);
            Assert.Equal(busy, Assert.Single((await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Key = mid.ToLowerInvariant() })).Keys).Fingerprint);
            Assert.Equal(busy, Assert.Single((await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Key = "0x" + busy[..12], Match = UsersAdminMatch.Prefix })).Keys).Fingerprint);
            var suffix = busy[^16..];
            Assert.Equal(busy, Assert.Single((await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Key = suffix[..8] + " " + suffix[8..], Match = UsersAdminMatch.Suffix })).Keys).Fingerprint);
            Assert.Empty((await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys, Key = mid, Match = UsersAdminMatch.Prefix })).Keys);
        }

        [Fact]
        public async Task Keys_BadgeModeratorOwnerLinkedAndBlocked()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var owned = await ReviewTestData.AddEntryAsync(h.Db);
            owned.PgpKey = KeyB.Value.Armored;
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            member.PgpFingerprint = KeyA.Value.Fingerprint;
            await h.Db.SaveChangesAsync();

            var plain = NewFingerprint();
            var review = PgpReview(entry, KeyA.Value.Fingerprint, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-3));
            h.Db.DirectoryEntryReviews.Add(review);
            h.Db.DirectoryEntryReviews.Add(PgpReview(entry, plain, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-2)));
            await h.Db.SaveChangesAsync();
            h.Db.DirectoryEntryReviewComments.Add(PgpReply(review, ModKey.Value.Fingerprint, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-1)));
            h.Db.DirectoryEntryReviewComments.Add(PgpReply(review, KeyB.Value.Fingerprint, ReviewModerationStatus.Approved, DateTime.UtcNow));
            await h.Db.SaveChangesAsync();

            var svc = Service(h, ModKey.Value.Armored);
            Assert.Equal(UsersAdminService.KeyBlockResult.Blocked, (await svc.BlockKeyAsync(plain, "spam", "owner@example.com", hidePosts: false)).Result);

            var rows = (await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys })).Keys.ToDictionary(k => k.Fingerprint);
            Assert.Equal(4, rows.Count);

            Assert.True(rows[ModKey.Value.Fingerprint].IsModerator);
            Assert.Empty(rows[ModKey.Value.Fingerprint].OwnerOf);

            var owner = rows[KeyB.Value.Fingerprint];
            Assert.False(owner.IsModerator);
            Assert.Equal(owned.DirectoryEntryId, Assert.Single(owner.OwnerOf).DirectoryEntryId);

            var linked = rows[KeyA.Value.Fingerprint];
            Assert.Equal("quiet_otter_4821", linked.LinkedMember!.Username);
            Assert.Empty(linked.OwnerOf);

            Assert.True(rows[plain].IsBlocked);
            Assert.False(rows[plain].IsModerator);
            Assert.Null(rows[plain].LinkedMember);
            Assert.Single((await svc.ListAsync(new UsersAdminQuery { Tab = UsersAdminTabs.Keys })).ActiveKeyBlocks);
        }

        // ------------------------------------------------------------------ detail pages

        [Fact]
        public async Task DetailPages_ListOnlyThatAuthorsPosts_NewestFirst_Paged()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var other = await h.CreateMemberAsync("brave_heron_1111");
            var fp = NewFingerprint();

            var memberReview = ReviewTestData.MemberReview(member, entry, ReviewModerationStatus.Approved);
            memberReview.OrderUrl = "https://alpha.example/orders/1";
            h.Db.DirectoryEntryReviews.Add(memberReview);
            h.Db.DirectoryEntryReviews.Add(ReviewTestData.MemberReview(other, entry, ReviewModerationStatus.Approved));
            var pgpReview = PgpReview(entry, fp, ReviewModerationStatus.Pending, DateTime.UtcNow.AddDays(-10));
            h.Db.DirectoryEntryReviews.Add(pgpReview);
            await h.Db.SaveChangesAsync();

            h.Db.DirectoryEntryReviewComments.Add(new DirectoryEntryReviewComment
            {
                DirectoryEntryReviewId = pgpReview.DirectoryEntryReviewId,
                Body = "member reply",
                AuthorFingerprint = MemberAuthor.AuthorKey(member.Id),
                AuthorUserId = member.Id,
                AuthorUsername = member.UserName,
                ModerationStatus = ReviewModerationStatus.Pending,
                CreateDate = DateTime.UtcNow,
            });
            for (int i = 0; i < 55; i++)
            {
                h.Db.DirectoryEntryReviewComments.Add(PgpReply(pgpReview, fp, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-5).AddMinutes(i)));
            }

            await h.Db.SaveChangesAsync();
            var svc = Service(h);

            var account = (await svc.GetAccountAsync(member.Id, 1))!;
            Assert.Equal(2, account.Posts.Total);
            Assert.True(account.Posts.Posts[0].IsReply); // newest first
            var review = account.Posts.Posts[1];
            Assert.False(review.IsReply);
            Assert.Equal(memberReview.DirectoryEntryReviewId, review.Id);
            Assert.True(review.HasOrderProof);
            Assert.Equal(entry.Name, review.EntryName);
            Assert.Equal(entry.DirectoryEntryKey, review.EntryKey);
            Assert.Equal((byte?)4, review.Rating);

            var key = (await svc.GetKeyAsync(fp.ToLowerInvariant(), 1))!;
            Assert.Equal(56, key.Posts.Total);
            Assert.Equal(2, key.Posts.TotalPages);
            Assert.Equal(50, key.Posts.Posts.Count);
            Assert.All(key.Posts.Posts, p => Assert.True(p.IsReply));
            var page2 = (await svc.GetKeyAsync(fp, 2))!;
            Assert.Equal(6, page2.Posts.Posts.Count);
            Assert.False(page2.Posts.Posts[^1].IsReply); // the oldest post is the review
            Assert.Equal(pgpReview.DirectoryEntryReviewId, page2.Posts.Posts[^1].Id);

            // Any full fingerprint opens (so a key can be blocked before it posts); junk does not.
            var unknown = (await svc.GetKeyAsync(NewFingerprint(), 1))!;
            Assert.True(unknown.HasNoPosts);
            Assert.Equal(0, unknown.Posts.Total);
            Assert.Null(await svc.GetKeyAsync("ABCD", 1));
            Assert.Null(await svc.GetAccountAsync("no-such-id", 1));
        }

        // ------------------------------------------------------------------ account blocks

        [Fact]
        public async Task BlockedAccount_CannotPostEditReplyOrLinkKey_UnblockRestores()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var reviews = ReviewTestData.Service(h.Db);
            var keys = KeyService(h);
            var existing = ReviewTestData.PgpReview(entry, ReviewModerationStatus.Approved);
            existing.AuthorFingerprint = NewFingerprint();
            h.Db.DirectoryEntryReviews.Add(existing);
            await h.Db.SaveChangesAsync();

            var first = await reviews.SaveReviewAsync(member, entry, Input());
            Assert.True(first.Succeeded, string.Join(", ", first.Errors.Values));

            var svc = Service(h);
            var (ok, _) = await svc.BlockAccountAsync(member.Id, "testing", "owner@example.com", hidePosts: false);
            Assert.True(ok);

            // The member instance in hand is stale: the block is read from the database.
            var edit = await reviews.SaveReviewAsync(member, entry, Input());
            Assert.Equal(ReviewerBlocks.MemberBlockedMessage, edit.Errors[string.Empty]);
            var reply = await reviews.SaveReplyAsync(member, existing.DirectoryEntryReviewId, new MemberReplyInputModel { Body = "A reply that is long enough to pass." });
            Assert.Equal(ReviewerBlocks.MemberBlockedMessage, reply.Errors[string.Empty]);
            var link = await keys.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.False(link.Succeeded);
            Assert.Equal(ReviewerBlocks.MemberBlockedMessage, link.Error);

            // The existing post was left alone (no "hide").
            Assert.Equal(ReviewModerationStatus.Pending, (await h.Db.DirectoryEntryReviews.AsNoTracking().SingleAsync(r => r.AuthorUserId == member.Id)).ModerationStatus);

            var detail = (await svc.GetAccountAsync(member.Id, 1))!;
            Assert.Equal("owner@example.com", detail.PostingBlockedBy);
            Assert.Equal("testing", detail.PostingBlockReason);
            Assert.True(detail.Account.IsPostingBlocked);

            Assert.True((await svc.UnblockAccountAsync(member.Id, "owner@example.com", restorePosts: false)).Ok);
            Assert.True((await reviews.SaveReplyAsync(member, existing.DirectoryEntryReviewId, new MemberReplyInputModel { Body = "A reply that is long enough to pass." })).Succeeded);
            Assert.True((await keys.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Succeeded);
            var after = (await svc.GetAccountAsync(member.Id, 1))!;
            Assert.Null(after.PostingBlockedUtc);
            Assert.Equal("owner@example.com", after.PostingUnblockedBy);
        }

        [Fact]
        public async Task AccountBlock_HidePosts_IsOptional_AndUnblockCanQueueThemAgain()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var published = ReviewTestData.MemberReview(member, entry, ReviewModerationStatus.Approved);
            var rejectedEarlier = ReviewTestData.MemberReview(member, await ReviewTestData.AddEntryAsync(h.Db), ReviewModerationStatus.Rejected);
            rejectedEarlier.RejectionReason = "spam";
            h.Db.DirectoryEntryReviews.AddRange(published, rejectedEarlier);
            await h.Db.SaveChangesAsync();

            var svc = Service(h);
            var (ok, changed) = await svc.BlockAccountAsync(member.Id, null, "owner@example.com", hidePosts: true);
            Assert.True(ok);
            Assert.Equal(new[] { published.DirectoryEntryReviewId }, changed);
            await ReloadAsync(h.Db, published, rejectedEarlier);
            Assert.Equal(ReviewModerationStatus.Rejected, published.ModerationStatus);
            Assert.Equal(ReviewerBlocks.AccountHiddenReason, published.RejectionReason);
            Assert.Equal(1, (await svc.GetAccountAsync(member.Id, 1))!.HiddenByBlockCount);

            var (unblocked, restored) = await svc.UnblockAccountAsync(member.Id, "owner@example.com", restorePosts: true);
            Assert.True(unblocked);
            Assert.Equal(1, restored);
            await ReloadAsync(h.Db, published, rejectedEarlier);
            Assert.Equal(ReviewModerationStatus.Pending, published.ModerationStatus); // back to the queue, not published
            Assert.Equal(ReviewModerationStatus.Rejected, rejectedEarlier.ModerationStatus); // an earlier rejection stays
            Assert.Equal("spam", rejectedEarlier.RejectionReason);
        }

        [Fact]
        public async Task AdminAccountsAndDeletedAccounts_CannotBeBlocked_LinkedKeyCanBeRemoved()
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var keys = KeyService(h);
            var start = await keys.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.True((await keys.ConfirmAsync(member, start.Pending!.Fingerprint, MemberPgpTestKeys.TryDecrypt(start.Pending.Ciphertext, KeyA.Value.Secrets))).Succeeded);

            var svc = Service(h);
            Assert.False((await svc.BlockAccountAsync(admin.Id, null, "x", false)).Ok);
            Assert.Null(await svc.RemoveLinkedKeyAsync(admin.Id));

            Assert.Equal(KeyA.Value.Fingerprint, await svc.RemoveLinkedKeyAsync(member.Id));
            Assert.Null((await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == member.Id)).PgpFingerprint);
            Assert.Null(await svc.RemoveLinkedKeyAsync(member.Id));

            // The freed key can be linked again (here by another account).
            var other = await h.CreateMemberAsync("brave_heron_1111");
            Assert.True((await keys.StartAsync(other, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Succeeded);
        }

        // ------------------------------------------------------------------ key blocks

        [Fact]
        public async Task BlockedKey_RefusedAtKeyStep_ForReviewsAndReplies()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var approved = ReviewTestData.PgpReview(entry, ReviewModerationStatus.Approved);
            approved.AuthorFingerprint = NewFingerprint();
            h.Db.DirectoryEntryReviews.Add(approved);
            await h.Db.SaveChangesAsync();
            Assert.Equal(UsersAdminService.KeyBlockResult.Blocked, (await Service(h).BlockKeyAsync(KeyA.Value.Fingerprint, null, "owner@example.com", false)).Result);

            var flows = new Flows(h);
            var reviewFlow = await flows.ReviewFlowAtKeyStepAsync(entry.DirectoryEntryId);
            var view = Assert.IsType<ViewResult>(await flows.Reviews.SubmitKeyPost(reviewFlow, KeyA.Value.Armored));
            Assert.Equal("SubmitKey", view.ViewName);
            Assert.Contains(ReviewerBlocks.KeyBlockedMessage, Errors(flows.Reviews));

            // Another key goes on to the code step as usual.
            var other = Assert.IsType<RedirectToActionResult>(await flows.Reviews.SubmitKeyPost(reviewFlow, KeyB.Value.Armored));
            Assert.Equal(nameof(DirectoryEntryReviewsController.VerifyCode), other.ActionName);

            var replyFlow = await flows.ReplyFlowAtKeyStepAsync(approved.DirectoryEntryReviewId);
            var replyView = Assert.IsType<ViewResult>(await flows.Replies.SubmitKeyPost(replyFlow, KeyA.Value.Armored));
            Assert.Equal("SubmitKey", replyView.ViewName);
            Assert.Contains(ReviewerBlocks.KeyBlockedMessage, Errors(flows.Replies));
        }

        [Fact]
        public async Task KeyBlockedAfterTheCodeStep_IsRefusedAtSubmit_UnblockRestoresPosting()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var flows = new Flows(h);
            var flowId = await flows.ReviewFlowAtKeyStepAsync(entry.DirectoryEntryId);
            Assert.IsType<RedirectToActionResult>(await flows.Reviews.SubmitKeyPost(flowId, KeyA.Value.Armored));
            flows.NewRequest(flows.Reviews);
            Assert.IsType<ViewResult>(flows.Reviews.VerifyCode(flowId));
            var code = MemberPgpTestKeys.TryDecrypt((string)flows.Reviews.ViewBag.Ciphertext, KeyA.Value.Secrets);
            flows.NewRequest(flows.Reviews);
            Assert.Equal(nameof(DirectoryEntryReviewsController.Compose), Assert.IsType<RedirectToActionResult>(flows.Reviews.VerifyCodePost(flowId, code!)).ActionName);

            var svc = Service(h);
            await svc.BlockKeyAsync(KeyA.Value.Fingerprint, null, "owner@example.com", false);

            flows.NewRequest(flows.Reviews);
            var refused = Assert.IsType<ViewResult>(await flows.Reviews.ComposePost(flowId, new DirectoryManager.Web.Models.CreateDirectoryEntryReviewInputModel { Body = ReviewTestData.GoodBody, Rating = 5 }, CancellationToken.None));
            Assert.Equal("~/Views/Shared/PostingUnavailable.cshtml", refused.ViewName);
            Assert.Equal(ReviewerBlocks.KeyBlockedMessage, refused.Model);
            Assert.Equal(0, await h.Db.DirectoryEntryReviews.CountAsync());

            // Unblocked: the same key gets through the key step again.
            Assert.True((await svc.UnblockKeyAsync(KeyA.Value.Fingerprint, "owner@example.com", restorePosts: false)).Ok);
            Assert.False(await ReviewerBlocks.IsKeyBlockedAsync(h.Db, KeyA.Value.Fingerprint));
            var again = await flows.ReviewFlowAtKeyStepAsync(entry.DirectoryEntryId);
            Assert.IsType<RedirectToActionResult>(await flows.Reviews.SubmitKeyPost(again, KeyA.Value.Armored));

            // The audit row stays, re-blocking reuses it.
            var row = await h.Db.BlockedReviewerKeys.AsNoTracking().SingleAsync();
            Assert.Equal("owner@example.com", row.UnblockedBy);
            Assert.Equal(UsersAdminService.KeyBlockResult.Blocked, (await svc.BlockKeyAsync(KeyA.Value.Fingerprint, "again", "owner@example.com", false)).Result);
            Assert.Equal(UsersAdminService.KeyBlockResult.AlreadyBlocked, (await svc.BlockKeyAsync(KeyA.Value.Fingerprint, "again", "owner@example.com", false)).Result);
            Assert.Equal(1, await h.Db.BlockedReviewerKeys.CountAsync());
        }

        [Fact]
        public async Task BlockedKey_CannotBeLinked_AndBlocksTheMemberHoldingIt()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var holder = await h.CreateMemberAsync("quiet_otter_4821");
            var other = await h.CreateMemberAsync("brave_heron_1111");
            var keys = KeyService(h);
            var reviews = ReviewTestData.Service(h.Db);

            var start = await keys.StartAsync(holder, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            var code = MemberPgpTestKeys.TryDecrypt(start.Pending!.Ciphertext, KeyA.Value.Secrets);

            var svc = Service(h);
            await svc.BlockKeyAsync(KeyB.Value.Fingerprint, null, "owner@example.com", false);
            var refused = await keys.StartAsync(other, IdentityHarness.MemberPassword, KeyB.Value.Armored);
            Assert.False(refused.Succeeded);
            Assert.Equal(ReviewerBlocks.KeyNotLinkableMessage, refused.Error);

            // Blocked while the code was out: the confirm is refused too.
            await svc.BlockKeyAsync(KeyA.Value.Fingerprint, null, "owner@example.com", false);
            var confirm = await keys.ConfirmAsync(holder, start.Pending.Fingerprint, code);
            Assert.False(confirm.Succeeded);
            Assert.Equal(ReviewerBlocks.KeyNotLinkableMessage, confirm.Error);

            // A member that already holds a key that gets blocked can't post.
            await svc.UnblockKeyAsync(KeyA.Value.Fingerprint, "owner@example.com", false);
            start = await keys.StartAsync(holder, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.True((await keys.ConfirmAsync(holder, start.Pending!.Fingerprint, MemberPgpTestKeys.TryDecrypt(start.Pending.Ciphertext, KeyA.Value.Secrets))).Succeeded);
            await svc.BlockKeyAsync(KeyA.Value.Fingerprint, null, "owner@example.com", false);
            var save = await reviews.SaveReviewAsync(holder, entry, Input());
            Assert.Equal(ReviewerBlocks.MemberBlockedMessage, save.Errors[string.Empty]);
            Assert.True((await svc.GetAccountAsync(holder.Id, 1))!.Account.LinkedKeyBlocked);

            await svc.UnblockKeyAsync(KeyA.Value.Fingerprint, "owner@example.com", false);
            Assert.True((await reviews.SaveReviewAsync(holder, entry, Input())).Succeeded);
        }

        [Fact]
        public async Task KeyLinkedToAMemberBlockedFromPosting_IsRefusedInThePgpFlow()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            member.PgpFingerprint = KeyA.Value.Fingerprint;
            await h.Db.SaveChangesAsync();
            await Service(h).BlockAccountAsync(member.Id, null, "owner@example.com", false);

            var flows = new Flows(h);
            var flowId = await flows.ReviewFlowAtKeyStepAsync(entry.DirectoryEntryId);
            Assert.IsType<ViewResult>(await flows.Reviews.SubmitKeyPost(flowId, KeyA.Value.Armored));
            Assert.Contains(ReviewerBlocks.KeyBlockedMessage, Errors(flows.Reviews));
        }

        [Fact]
        public async Task KeyBlock_HidePosts_OnlyPgpPostsOfThatKey_UnblockCanQueueThemAgain()
        {
            using var h = new IdentityHarness();
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            member.PgpFingerprint = KeyA.Value.Fingerprint;
            var fp = KeyA.Value.Fingerprint;
            var pgp = PgpReview(entry, fp, ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-2));
            var otherKey = PgpReview(entry, NewFingerprint(), ReviewModerationStatus.Approved, DateTime.UtcNow.AddDays(-2));
            var memberPost = ReviewTestData.MemberReview(member, await ReviewTestData.AddEntryAsync(h.Db), ReviewModerationStatus.Approved);
            h.Db.DirectoryEntryReviews.AddRange(pgp, otherKey, memberPost);
            await h.Db.SaveChangesAsync();
            var reply = PgpReply(otherKey, fp, ReviewModerationStatus.Pending, DateTime.UtcNow);
            h.Db.DirectoryEntryReviewComments.Add(reply);
            await h.Db.SaveChangesAsync();

            var svc = Service(h);
            var (result, changed) = await svc.BlockKeyAsync(fp, "spam", "owner@example.com", hidePosts: true);
            Assert.Equal(UsersAdminService.KeyBlockResult.Blocked, result);
            Assert.Equal(new[] { pgp.DirectoryEntryReviewId }, changed); // the pending reply changed no public page

            await ReloadAsync(h.Db, pgp, otherKey, memberPost);
            await h.Db.Entry(reply).ReloadAsync();
            Assert.Equal(ReviewModerationStatus.Rejected, pgp.ModerationStatus);
            Assert.Equal(ReviewModerationStatus.Rejected, reply.ModerationStatus);
            Assert.Equal(ReviewModerationStatus.Approved, otherKey.ModerationStatus);
            Assert.Equal(ReviewModerationStatus.Approved, memberPost.ModerationStatus); // member posts untouched
            Assert.Equal(2, (await svc.GetKeyAsync(fp, 1))!.HiddenByBlockCount);

            var (ok, restored) = await svc.UnblockKeyAsync(fp, "owner@example.com", restorePosts: true);
            Assert.True(ok);
            Assert.Equal(2, restored);
            await ReloadAsync(h.Db, pgp);
            await h.Db.Entry(reply).ReloadAsync();
            Assert.Equal(ReviewModerationStatus.Pending, pgp.ModerationStatus);
            Assert.Equal(ReviewModerationStatus.Pending, reply.ModerationStatus);
        }

        [Fact]
        public async Task ModeratorKey_CannotBeBlocked_JunkIsRefused()
        {
            using var h = new IdentityHarness();
            var svc = Service(h, ModKey.Value.Armored);

            var (result, _) = await svc.BlockKeyAsync(ModKey.Value.Fingerprint, null, "owner@example.com", hidePosts: true);
            Assert.Equal(UsersAdminService.KeyBlockResult.ModeratorKey, result);

            // Also when given as the moderator key's primary fingerprint (any key of its ring).
            var primary = MemberPgpTestKeys.Hex(ModKey.Value.Ring.GetPublicKey().GetFingerprint());
            Assert.Equal(UsersAdminService.KeyBlockResult.ModeratorKey, (await svc.BlockKeyAsync(primary, null, "owner@example.com", false)).Result);
            Assert.Equal(0, await h.Db.BlockedReviewerKeys.CountAsync());

            Assert.Equal(UsersAdminService.KeyBlockResult.InvalidFingerprint, (await svc.BlockKeyAsync("ABCD1234", null, "x", false)).Result);
            Assert.Equal(UsersAdminService.KeyBlockResult.InvalidFingerprint, (await svc.BlockKeyAsync(null, null, "x", false)).Result);
        }

        [Fact]
        public async Task Controller_RequiresConfirm_AndRefusesTheModeratorKey()
        {
            using var h = new IdentityHarness();
            var admin = await h.CreateUserAsync("owner@example.com");
            await h.SignInPrincipalAsync(admin);
            var c = new UsersAdminController(
                Service(h, ModKey.Value.Armored),
                KeyService(h),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<UsersAdminController>.Instance,
                h.Services.GetRequiredService<IMemoryCache>());
            c.ControllerContext = new ControllerContext { HttpContext = h.HttpContext };
            c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(h.HttpContext, new NullTempDataProvider());

            var fp = NewFingerprint();
            await c.BlockKey(fp, "x", hidePosts: false, confirm: false, CancellationToken.None);
            Assert.Equal(UsersAdminController.ConfirmMessage, c.TempData["UsersError"]);
            Assert.False(await ReviewerBlocks.IsKeyBlockedAsync(h.Db, fp));

            await c.BlockKey(ModKey.Value.Fingerprint, "x", hidePosts: false, confirm: true, CancellationToken.None);
            Assert.Contains("moderator key", (string)c.TempData["UsersError"]!, StringComparison.Ordinal);
            Assert.Equal(0, await h.Db.BlockedReviewerKeys.CountAsync());

            var member = await h.CreateMemberAsync("quiet_otter_4821");
            await c.BlockAccount(member.Id, "x", hidePosts: false, confirm: false, CancellationToken.None);
            Assert.Null((await h.Db.Users.AsNoTracking().SingleAsync(u => u.Id == member.Id)).PostingBlockedUtc);
            await c.RemoveKey(member.Id, confirm: false, CancellationToken.None);
            Assert.Equal(UsersAdminController.ConfirmMessage, c.TempData["UsersError"]);
        }

        [Fact]
        public async Task OldMembersUrl_OpensTheAccountsTab()
        {
            using var h = new IdentityHarness();
            var c = new MembersAdminController(h.Members, Microsoft.Extensions.Logging.Abstractions.NullLogger<MembersAdminController>.Instance, h.Services.GetRequiredService<IMemoryCache>());
            var r = Assert.IsType<RedirectResult>(c.Index(null));
            Assert.Equal("/admin/users?tab=accounts", r.Url);
            await Task.CompletedTask;
        }

        // ------------------------------------------------------------------ Postgres translation

        [Fact]
        public void Queries_TranslateToPostgresSql()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=not_used;Username=not_used;Password=not_used")
                .Options;
            using var db = new ApplicationDbContext(options);

            foreach (var sort in new[] { UsersAdminSorts.Newest, UsersAdminSorts.MostPosts, UsersAdminSorts.LastActive })
            {
                var accounts = UsersAdminService.SortAccounts(
                    UsersAdminService.AccountAggregates(db, "bee", "ABCD", UsersAdminMatch.Suffix, UsersAdminStatuses.Active), sort)
                    .Skip(50).Take(50).ToQueryString();
                Assert.Contains("LIMIT", accounts, StringComparison.Ordinal);
                Assert.Contains("OFFSET", accounts, StringComparison.Ordinal);

                var keys = UsersAdminService.SortKeys(UsersAdminService.KeyAggregates(db, "ABCD", UsersAdminMatch.Prefix), sort)
                    .Skip(50).Take(50).ToQueryString();
                Assert.Contains("GROUP BY", keys, StringComparison.Ordinal);
                Assert.Contains("UNION ALL", keys, StringComparison.Ordinal);
                Assert.Contains("LIMIT", keys, StringComparison.Ordinal);
            }

            var posts = UsersAdminService.PostsQuery(db, null, "ABCD").Skip(50).Take(50).ToQueryString();
            Assert.Contains("UNION ALL", posts, StringComparison.Ordinal);
            Assert.Contains("LIMIT", posts, StringComparison.Ordinal);
            Assert.Contains("LIMIT", UsersAdminService.PostsQuery(db, "some-user", null).Take(50).ToQueryString(), StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------ helpers

        private static UsersAdminService Service(IdentityHarness h, string? moderatorKey = null) =>
            new (h.Db, new MemberPgpTestKeys.SiteConfig(moderatorKey), h.Services.GetRequiredService<IMemoryCache>());

        private static MemberPgpKeyService KeyService(IdentityHarness h) =>
            new (h.Db, h.Members, new PgpService(), new MemberPgpTestKeys.SiteConfig(null), h.Services.GetRequiredService<IMemoryCache>());

        private static MemberReviewInputModel Input() => new ()
        {
            Rating = 4,
            Body = ReviewTestData.GoodBody + " " + Guid.NewGuid().ToString("N"),
        };

        private static string NewFingerprint() =>
            (Interlocked.Increment(ref nextFp).ToString("X8") + Guid.NewGuid().ToString("N")).ToUpperInvariant()[..40];

        private static ApplicationUser AddUser(ApplicationDbContext db, string name, DateTime created)
        {
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = name,
                NormalizedUserName = name.ToUpperInvariant(),
                CreateDate = created,
                SecurityStamp = Guid.NewGuid().ToString(),
            };
            db.Users.Add(u);
            return u;
        }

        private static DirectoryEntryReview PgpReview(DirectoryEntry entry, string fp, ReviewModerationStatus status, DateTime created) => new ()
        {
            DirectoryEntryId = entry.DirectoryEntryId,
            AuthorFingerprint = fp,
            Rating = 5,
            Body = "A PGP review. " + Guid.NewGuid().ToString("N"),
            ModerationStatus = status,
            CreateDate = created,
        };

        private static DirectoryEntryReviewComment PgpReply(DirectoryEntryReview review, string fp, ReviewModerationStatus status, DateTime created) => new ()
        {
            DirectoryEntryReviewId = review.DirectoryEntryReviewId,
            AuthorFingerprint = fp,
            Body = "A PGP reply. " + Guid.NewGuid().ToString("N"),
            ModerationStatus = status,
            CreateDate = created,
        };

        private static async Task ReloadAsync(ApplicationDbContext db, params DirectoryEntryReview[] reviews)
        {
            foreach (var r in reviews)
            {
                await db.Entry(r).ReloadAsync();
            }
        }

        private static List<string> Errors(Controller c) =>
            c.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();

        /// <summary>The real PGP review / reply controllers over the harness database and a real cache.</summary>
        private sealed class Flows
        {
            private readonly IdentityHarness h;
            private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
            private readonly IDataProtectionProvider dataProtection = new EphemeralDataProtectionProvider();

            public Flows(IdentityHarness h)
            {
                this.h = h;
                var entries = new DirectoryEntryRepository(h.Db, new DirectoryEntriesAuditRepository(h.Db));
                var reviewRepo = new DirectoryEntryReviewRepository(h.Db);
                this.Reviews = new DirectoryEntryReviewsController(reviewRepo, this.cache, new AcceptCaptcha(), new PgpService(), entries, null!, null!, null!, null!, null!, this.dataProtection);
                this.Replies = new DirectoryEntryReviewRepliesController(this.cache, new AcceptCaptcha(), new PgpService(), reviewRepo, null!, entries, null!, null!, null!, this.dataProtection);
                this.NewRequest(this.Reviews);
                this.NewRequest(this.Replies);
            }

            public DirectoryEntryReviewsController Reviews { get; }

            public DirectoryEntryReviewRepliesController Replies { get; }

            public void NewRequest(Controller c)
            {
                var http = new DefaultHttpContext { RequestServices = this.h.Services };
                c.ControllerContext = new ControllerContext(new ActionContext(
                    http,
                    new Microsoft.AspNetCore.Routing.RouteData(),
                    new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()));
                c.ModelState.Clear();
            }

            public async Task<Guid> ReviewFlowAtKeyStepAsync(int entryId)
            {
                this.NewRequest(this.Reviews);
                var begin = Assert.IsType<RedirectToActionResult>(await this.Reviews.Begin(entryId, website: null));
                var flowId = (Guid)begin.RouteValues!["flowId"]!;
                var ticket = (string)begin.RouteValues![PreCaptchaFlowTicket.FieldName]!;
                Assert.IsType<RedirectToActionResult>(this.Reviews.CaptchaPost(flowId, ticket));
                this.NewRequest(this.Reviews);
                return flowId;
            }

            public async Task<Guid> ReplyFlowAtKeyStepAsync(int reviewId)
            {
                this.NewRequest(this.Replies);
                var begin = Assert.IsType<RedirectToActionResult>(await this.Replies.Begin(reviewId, website: null, CancellationToken.None));
                var flowId = (Guid)begin.RouteValues!["flowId"]!;
                var ticket = (string)begin.RouteValues![PreCaptchaFlowTicket.FieldName]!;
                Assert.IsType<RedirectToActionResult>(this.Replies.CaptchaPost(flowId, ticket));
                this.NewRequest(this.Replies);
                return flowId;
            }
        }

        private sealed class AcceptCaptcha : ICaptchaService
        {
            public bool IsValid(HttpRequest request) => true;
        }

        private sealed class NullTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

            public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            {
            }
        }
    }
}
