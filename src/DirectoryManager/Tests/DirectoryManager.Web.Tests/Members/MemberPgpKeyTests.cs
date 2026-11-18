using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.Members;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using DirectoryManager.Web.Tests.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace DirectoryManager.Web.Tests.Members
{
    /// <summary>
    /// Linking a PGP key to a member account: the same challenge as the PGP review flow, bound to
    /// the member and the key, display-only (never owner / moderator powers).
    /// </summary>
    public class MemberPgpKeyTests
    {
        // Keys are slow-ish to generate; share a few across tests (public material + test secrets only).
        private static readonly Lazy<MemberPgpTestKeys.TestKey> KeyA = new (() => MemberPgpTestKeys.Generate("A <a@example.com>"));
        private static readonly Lazy<MemberPgpTestKeys.TestKey> KeyB = new (() => MemberPgpTestKeys.Generate("B <b@example.com>"));

        [Fact]
        public async Task Link_HappyPath_SavesTheChallengeKeysFingerprint_CodeIsOneTime()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            var start = await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.True(start.Succeeded, start.Error);
            var pending = start.Pending!;

            // Identity = the very key the code was encrypted to (same rule as the review flow).
            Assert.Equal(KeyA.Value.Fingerprint, pending.Fingerprint);
            var recipient = KeyA.Value.Ring.GetPublicKey(MemberPgpTestKeys.RecipientKeyId(pending.Ciphertext));
            Assert.Equal(pending.Fingerprint, MemberPgpTestKeys.Hex(recipient.GetFingerprint()));

            var code = MemberPgpTestKeys.TryDecrypt(pending.Ciphertext, KeyA.Value.Secrets);
            Assert.NotNull(code);

            var confirm = await svc.ConfirmAsync(member, pending.Fingerprint, code!.ToLowerInvariant().Replace("-", " "));
            Assert.True(confirm.Succeeded, confirm.Error);

            var saved = await h.UserManager.FindByIdAsync(member.Id);
            Assert.Equal(KeyA.Value.Fingerprint, saved!.PgpFingerprint);
            Assert.NotNull(saved.PgpLinkedUtc);

            // One-time: the same code can't be replayed.
            Assert.Null(svc.GetPending(member));
            var replay = await svc.ConfirmAsync(member, pending.Fingerprint, code);
            Assert.False(replay.Succeeded);
        }

        [Fact]
        public async Task Start_NeedsThePassword_AndAValidKey_NoStateOtherwise()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            var wrongPw = await svc.StartAsync(member, "not my password", KeyA.Value.Armored);
            Assert.False(wrongPw.Succeeded);
            Assert.Equal(MemberPgpKeyService.WrongPasswordMessage, wrongPw.Error);

            var garbage = await svc.StartAsync(member, IdentityHarness.MemberPassword, "-----BEGIN PGP PUBLIC KEY BLOCK-----\nnope\n");
            Assert.Equal(MemberPgpKeyService.InvalidKeyMessage, garbage.Error);

            var huge = await svc.StartAsync(member, IdentityHarness.MemberPassword, new string('A', IntegerConstants.MaxPgpArmoredChars + 1));
            Assert.Equal(MemberPgpKeyService.InvalidKeyMessage, huge.Error);

            Assert.Null(svc.GetPending(member));
        }

        [Fact]
        public async Task WrongCode_IsRefused_AndAttemptsAreBounded()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var pending = (await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Pending!;
            var code = MemberPgpTestKeys.TryDecrypt(pending.Ciphertext, KeyA.Value.Secrets)!;

            for (int i = 0; i < IntegerConstants.MaxVerifyAttempts; i++)
            {
                var wrong = await svc.ConfirmAsync(member, pending.Fingerprint, "AAAAA-AAAAA");
                Assert.False(wrong.Succeeded);
                Assert.Equal(MemberPgpKeyService.WrongCodeMessage, wrong.Error);
            }

            // Out of attempts: even the right code no longer works, and the challenge is gone.
            var late = await svc.ConfirmAsync(member, pending.Fingerprint, code);
            Assert.False(late.Succeeded);
            Assert.Equal(MemberPgpKeyService.TooManyAttemptsMessage, late.Error);
            Assert.Null(svc.GetPending(member));
            Assert.Null((await h.UserManager.FindByIdAsync(member.Id))!.PgpFingerprint);
        }

        [Fact]
        public async Task ExpiredChallenge_IsRefused()
        {
            using var h = new IdentityHarness();
            var (svc, _, clock) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var pending = (await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Pending!;
            var code = MemberPgpTestKeys.TryDecrypt(pending.Ciphertext, KeyA.Value.Secrets)!;

            clock.Advance(TimeSpan.FromMinutes(MemberPgpKeyService.ChallengeMinutes + 1));

            var result = await svc.ConfirmAsync(member, pending.Fingerprint, code);
            Assert.False(result.Succeeded);
            Assert.Equal(MemberPgpKeyService.ExpiredMessage, result.Error);
            Assert.Null((await h.UserManager.FindByIdAsync(member.Id))!.PgpFingerprint);
        }

        [Fact]
        public async Task ChallengeForKeyA_CannotConfirmKeyB()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            // A code for key A, presented as confirming key B: refused, and the challenge is dropped.
            var a = (await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Pending!;
            var codeA = MemberPgpTestKeys.TryDecrypt(a.Ciphertext, KeyA.Value.Secrets)!;
            var crossed = await svc.ConfirmAsync(member, KeyB.Value.Fingerprint, codeA);
            Assert.False(crossed.Succeeded);
            Assert.Equal(MemberPgpKeyService.OtherKeyMessage, crossed.Error);

            // Start A, then start B (replaces A's challenge): A's code can't confirm anything any more.
            a = (await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Pending!;
            codeA = MemberPgpTestKeys.TryDecrypt(a.Ciphertext, KeyA.Value.Secrets)!;
            var b = (await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyB.Value.Armored)).Pending!;
            Assert.Null(MemberPgpTestKeys.TryDecrypt(b.Ciphertext, KeyA.Value.Secrets)); // A can't read B's code

            Assert.False((await svc.ConfirmAsync(member, a.Fingerprint, codeA)).Succeeded);
            Assert.Null((await h.UserManager.FindByIdAsync(member.Id))!.PgpFingerprint);
        }

        [Fact]
        public async Task ForgedRing_AttackerReBindsVictimSubkey_CannotLinkTheVictimsIdentity()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var attacker = await h.CreateMemberAsync("sly_fox_6666");
            var (forged, attackerSecrets, victimSubkey) = MemberPgpTestKeys.ForgeRingAroundVictimSubkey();
            var victimFp = MemberPgpTestKeys.Hex(victimSubkey.GetFingerprint());

            var start = await svc.StartAsync(attacker, IdentityHarness.MemberPassword, forged);
            Assert.True(start.Succeeded, start.Error);

            // Named as the victim => the code goes to the victim's key, which the attacker can't read.
            Assert.Equal(victimFp, start.Pending!.Fingerprint);
            Assert.Equal(victimSubkey.KeyId, MemberPgpTestKeys.RecipientKeyId(start.Pending.Ciphertext));
            Assert.Null(MemberPgpTestKeys.TryDecrypt(start.Pending.Ciphertext, attackerSecrets));

            // Guessing gets nowhere within the attempt limit.
            for (int i = 0; i <= IntegerConstants.MaxVerifyAttempts; i++)
            {
                Assert.False((await svc.ConfirmAsync(attacker, victimFp, "ABCDE-FGHIJ")).Succeeded);
            }

            Assert.Null((await h.UserManager.FindByIdAsync(attacker.Id))!.PgpFingerprint);
        }

        [Fact]
        public async Task OneKey_CanBeLinkedToOnlyOneAccount()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var first = await h.CreateMemberAsync("quiet_otter_4821");
            var second = await h.CreateMemberAsync("brave_heron_1111");

            // Second starts first, then the first account links the key: the second's confirm is refused.
            var pending2 = (await svc.StartAsync(second, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Pending!;
            await LinkAsync(svc, first, KeyA.Value);
            var code2 = MemberPgpTestKeys.TryDecrypt(pending2.Ciphertext, KeyA.Value.Secrets)!;
            var raced = await svc.ConfirmAsync(second, pending2.Fingerprint, code2);
            Assert.False(raced.Succeeded);
            Assert.Equal(MemberPgpKeyService.LinkedElsewhereMessage, raced.Error);

            // And it can't even start now.
            var again = await svc.StartAsync(second, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.Equal(MemberPgpKeyService.LinkedElsewhereMessage, again.Error);
            Assert.Null((await h.UserManager.FindByIdAsync(second.Id))!.PgpFingerprint);
        }

        [Fact]
        public async Task DeletingTheAccount_FreesTheKey()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var first = await h.CreateMemberAsync("quiet_otter_4821");
            await LinkAsync(svc, first, KeyA.Value);
            Assert.True(await h.Members.DeleteAccountAsync(first, IdentityHarness.MemberPassword));
            Assert.Null((await h.UserManager.FindByIdAsync(first.Id))!.PgpFingerprint);

            var second = await h.CreateMemberAsync("brave_heron_1111");
            await LinkAsync(svc, second, KeyA.Value);
        }

        [Fact]
        public async Task ModeratorKey_IsRefused()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h, moderatorKey: KeyA.Value.Armored);
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            var result = await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.False(result.Succeeded);
            Assert.Equal(MemberPgpKeyService.ModeratorKeyMessage, result.Error);
            Assert.Null(svc.GetPending(member));

            // Another key is fine.
            Assert.True((await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyB.Value.Armored)).Succeeded);
        }

        [Fact]
        public async Task ModeratorKey_SetAfterTheCodeWasSent_IsStillRefusedAtConfirm()
        {
            using var h = new IdentityHarness();
            var (plain, cache, clock) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var pending = (await plain.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Pending!;
            var code = MemberPgpTestKeys.TryDecrypt(pending.Ciphertext, KeyA.Value.Secrets)!;

            var withModerator = new MemberPgpKeyService(h.Db, h.Members, new PgpService(), new MemberPgpTestKeys.SiteConfig(KeyA.Value.Armored), cache, clock);
            var result = await withModerator.ConfirmAsync(member, pending.Fingerprint, code);
            Assert.False(result.Succeeded);
            Assert.Null((await h.UserManager.FindByIdAsync(member.Id))!.PgpFingerprint);
        }

        [Fact]
        public async Task DisplayLabel_FollowsTheAccountsCurrentKey_BeforeLinkAfterLinkAfterRemoval()
        {
            using var h = new IdentityHarness();
            var (svc, cache, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var directory = new MemberKeyDirectory(h.Db, cache);

            // Before: "not PGP-verified" (and the lookup is now cached).
            Assert.Null(await directory.FingerprintForAsync(member.Id));
            Assert.Equal(MemberAuthor.PublicLabel, MemberAuthor.LabelFor(await directory.FingerprintForAsync(member.Id)));

            await LinkAsync(svc, member, KeyA.Value);
            var fp = await directory.FingerprintForAsync(member.Id);
            Assert.Equal(KeyA.Value.Fingerprint, fp);
            Assert.Equal(MemberAuthor.VerifiedLabel, MemberAuthor.LabelFor(fp));
            Assert.Contains("PGP-verified", MemberAuthor.VerifiedLabel);
            Assert.DoesNotContain("not", MemberAuthor.VerifiedLabel);
            Assert.Equal("Member: quiet_otter_4821 (linked PGP key " + fp + ")", MemberAuthor.ForAdmin("usr:x", member.Id, member.UserName, fp));

            // Remove needs the password.
            var refused = await svc.RemoveAsync(member, "wrong password here");
            Assert.False(refused.Succeeded);
            Assert.Equal(KeyA.Value.Fingerprint, await directory.FingerprintForAsync(member.Id));

            Assert.True((await svc.RemoveAsync(member, IdentityHarness.MemberPassword)).Succeeded);
            Assert.Null(await directory.FingerprintForAsync(member.Id));
            Assert.Equal(MemberAuthor.PublicLabel, MemberAuthor.LabelFor(await directory.FingerprintForAsync(member.Id)));

            // PGP-flow posts and empty ids never get a member key.
            Assert.Null(await directory.FingerprintForAsync(null));
        }

        [Fact]
        public async Task ReplaceKey_RequiresProvingTheNewKey_OldKeyStaysUntilThen()
        {
            using var h = new IdentityHarness();
            var (svc, cache, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var directory = new MemberKeyDirectory(h.Db, cache);
            await LinkAsync(svc, member, KeyA.Value);

            var pending = (await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyB.Value.Armored)).Pending!;
            Assert.Equal(KeyA.Value.Fingerprint, await directory.FingerprintForAsync(member.Id));
            Assert.False((await svc.ConfirmAsync(member, pending.Fingerprint, "AAAAA-AAAAA")).Succeeded);
            Assert.Equal(KeyA.Value.Fingerprint, (await h.UserManager.FindByIdAsync(member.Id))!.PgpFingerprint);

            var code = MemberPgpTestKeys.TryDecrypt(pending.Ciphertext, KeyB.Value.Secrets)!;
            Assert.True((await svc.ConfirmAsync(member, pending.Fingerprint, code)).Succeeded);
            Assert.Equal(KeyB.Value.Fingerprint, await directory.FingerprintForAsync(member.Id));

            // The old key is free again.
            var other = await h.CreateMemberAsync("brave_heron_1111");
            await LinkAsync(svc, other, KeyA.Value);
        }

        [Fact]
        public async Task DuplicateReviewRule_AppliesAcrossTheTwoPaths()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var reviews = ReviewTestData.Service(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var reviewedWithKey = await ReviewTestData.AddEntryAsync(h.Db);
            var other = await ReviewTestData.AddEntryAsync(h.Db);

            // Key A already reviewed this listing through the no-account PGP flow.
            var pgpReview = ReviewTestData.PgpReview(reviewedWithKey, ReviewModerationStatus.Approved);
            pgpReview.AuthorFingerprint = KeyA.Value.Fingerprint;
            h.Db.DirectoryEntryReviews.Add(pgpReview);
            await h.Db.SaveChangesAsync();

            // Before linking, the account is a different reviewer.
            Assert.False(await ReviewerKeyRules.KeyHasPgpReviewAsync(h.Db, member.PgpFingerprint, reviewedWithKey.DirectoryEntryId));

            await LinkAsync(svc, member, KeyA.Value);

            // Member path: no second review of that listing with the same key.
            var blocked = await reviews.SaveReviewAsync(member, reviewedWithKey, new MemberReviewInputModel { Rating = 4, Body = ReviewTestData.GoodBody });
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Errors, e => e.Value == ReviewerKeyRules.MemberAlreadyReviewedWithKeyMessage);

            // Another listing is fine.
            var ok = await reviews.SaveReviewAsync(member, other, new MemberReviewInputModel { Rating = 4, Body = ReviewTestData.GoodBody });
            Assert.True(ok.Succeeded);

            // PGP path: the same key can't add a review where the member account already reviewed.
            Assert.True(await ReviewerKeyRules.KeyHasMemberReviewAsync(h.Db, KeyA.Value.Fingerprint, other.DirectoryEntryId));
            Assert.True(await ReviewerKeyRules.KeyHasMemberReviewAsync(h.Db, KeyA.Value.Fingerprint.ToLowerInvariant(), other.DirectoryEntryId));
            Assert.False(await ReviewerKeyRules.KeyHasMemberReviewAsync(h.Db, KeyB.Value.Fingerprint, other.DirectoryEntryId));
            Assert.False(await ReviewerKeyRules.KeyHasMemberReviewAsync(h.Db, KeyA.Value.Fingerprint, reviewedWithKey.DirectoryEntryId + 99999));

            // Nothing was merged or rewritten.
            Assert.Equal(KeyA.Value.Fingerprint, h.Db.DirectoryEntryReviews.Single(r => r.DirectoryEntryReviewId == pgpReview.DirectoryEntryReviewId).AuthorFingerprint);
            Assert.Null(h.Db.DirectoryEntryReviews.Single(r => r.DirectoryEntryReviewId == pgpReview.DirectoryEntryReviewId).AuthorUserId);

            // After unlinking, the account and the key are separate reviewers again.
            Assert.True((await svc.RemoveAsync(member, IdentityHarness.MemberPassword)).Succeeded);
            Assert.False(await ReviewerKeyRules.KeyHasMemberReviewAsync(h.Db, KeyA.Value.Fingerprint, other.DirectoryEntryId));
        }

        [Fact]
        public async Task MemberWithTheListingOwnersKey_PostsStayPending_NeverOwnerOrModerator()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var reviews = ReviewTestData.Service(h.Db);
            var member = await h.CreateMemberAsync("quiet_otter_4821");
            var entry = await ReviewTestData.AddEntryAsync(h.Db);
            entry.PgpKey = KeyA.Value.Armored; // the listing owner's key
            await h.Db.SaveChangesAsync();

            await LinkAsync(svc, member, KeyA.Value);

            var saved = await reviews.SaveReviewAsync(member, entry, new MemberReviewInputModel { Rating = 5, Body = ReviewTestData.GoodBody });
            Assert.True(saved.Succeeded);
            var review = h.Db.DirectoryEntryReviews.Single(r => r.DirectoryEntryReviewId == saved.Id);
            Assert.Equal(ReviewModerationStatus.Pending, review.ModerationStatus);
            Assert.StartsWith(MemberAuthor.KeyPrefix, review.AuthorFingerprint);
            Assert.NotEqual(KeyA.Value.Fingerprint, review.AuthorFingerprint);

            // Even once a moderator approves it, the owner rule never matches the member's post.
            review.ModerationStatus = ReviewModerationStatus.Approved;
            review.DirectoryEntry = entry;
            await h.Db.SaveChangesAsync();
            var pgpFlowControl = new DirectoryEntryReview { AuthorFingerprint = KeyA.Value.Fingerprint, DirectoryEntry = entry, DirectoryEntryId = entry.DirectoryEntryId };
            ReviewAuthorFlags.ApplyOwnerFlags(new[] { review, pgpFlowControl });
            Assert.True(pgpFlowControl.IsOwner); // the same key through the PGP flow IS the owner (control)
            Assert.False(review.IsOwner);

            var reply = await reviews.SaveReplyAsync(member, review.DirectoryEntryReviewId, new MemberReplyInputModel { Body = "Thanks, that matches my experience with them as well." });
            Assert.True(reply.Succeeded);
            var c = h.Db.DirectoryEntryReviewComments.Single(x => x.DirectoryEntryReviewCommentId == reply.Id);
            Assert.Equal(ReviewModerationStatus.Pending, c.ModerationStatus);
            Assert.StartsWith(MemberAuthor.KeyPrefix, c.AuthorFingerprint);
            Assert.DoesNotContain(PgpFingerprintTools.GetAllFingerprints(KeyA.Value.Armored), fp => PgpFingerprintTools.Matches(c.AuthorFingerprint, fp));
        }

        [Fact]
        public async Task NewChallenges_AreCappedPerMember_OnePendingAtATime()
        {
            using var h = new IdentityHarness();
            var (svc, _, _) = Service(h);
            var member = await h.CreateMemberAsync("quiet_otter_4821");

            for (int i = 0; i < MemberPgpKeyService.MaxChallengesPerHour; i++)
            {
                Assert.True((await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored)).Succeeded);
            }

            var capped = await svc.StartAsync(member, IdentityHarness.MemberPassword, KeyA.Value.Armored);
            Assert.Equal(MemberPgpKeyService.TooManyChallengesMessage, capped.Error);
        }

        [Fact]
        public void PgpKeyServices_AreRegisteredWithTheMemberAccounts()
        {
            using var h = new IdentityHarness();
            Assert.NotNull(h.Services.GetRequiredService<MemberPgpKeyService>());
            Assert.NotNull(h.Services.GetRequiredService<MemberKeyDirectory>());
        }

        private static (MemberPgpKeyService Service, IMemoryCache Cache, MutableClock Clock) Service(IdentityHarness h, string? moderatorKey = null)
        {
            var cache = h.Services.GetRequiredService<IMemoryCache>();
            var clock = new MutableClock(DateTimeOffset.UtcNow);
            return (new MemberPgpKeyService(h.Db, h.Members, new PgpService(), new MemberPgpTestKeys.SiteConfig(moderatorKey), cache, clock), cache, clock);
        }

        private static async Task LinkAsync(MemberPgpKeyService svc, ApplicationUser member, MemberPgpTestKeys.TestKey key)
        {
            var start = await svc.StartAsync(member, IdentityHarness.MemberPassword, key.Armored);
            Assert.True(start.Succeeded, start.Error);
            var code = MemberPgpTestKeys.TryDecrypt(start.Pending!.Ciphertext, key.Secrets);
            var confirm = await svc.ConfirmAsync(member, start.Pending.Fingerprint, code);
            Assert.True(confirm.Succeeded, confirm.Error);
        }
    }
}
