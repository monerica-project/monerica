using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class SubmissionProtectedFieldsTests
    {
        private const string Key = "-----BEGIN PGP PUBLIC KEY BLOCK-----\nAAAA\n-----END PGP PUBLIC KEY BLOCK-----";

        private static DirectoryEntry Entry() => new DirectoryEntry
        {
            Name = "x", DirectoryEntryKey = "x", Link = "https://xmr2cex.com", Email = "a@b.c", PgpKey = Key, DirectoryStatus = DirectoryStatus.Verified,
        };

        [Fact]
        public void KeepProtected_PublicEditCannotSetScamOrSwapKey()
        {
            var sub = new Submission { DirectoryStatus = DirectoryStatus.Scam, PgpKey = "attacker key" };
            SubmissionProtectedFields.KeepProtected(sub, Entry());
            Assert.Equal(DirectoryStatus.Verified, sub.DirectoryStatus);
            Assert.Equal(Key, sub.PgpKey);
        }

        [Fact]
        public void KeepProtected_PublicEditCannotClearKey()
        {
            var sub = new Submission { PgpKey = string.Empty };
            SubmissionProtectedFields.KeepProtected(sub, Entry());
            Assert.Equal(Key, sub.PgpKey);
        }

        [Fact]
        public void KeepProtected_ListingWithoutKeyMayGetOne()
        {
            var entry = Entry();
            entry.PgpKey = null;
            var sub = new Submission { PgpKey = "new key" };
            SubmissionProtectedFields.KeepProtected(sub, entry);
            Assert.Equal("new key", sub.PgpKey);
        }

        [Fact]
        public void SensitiveChanges_FlagsLinkEmailKeyAndStatus()
        {
            var sub = new Submission
            {
                Link = "https://example.org/", Email = "probe@example.org", PgpKey = "other", DirectoryStatus = DirectoryStatus.Scam,
            };
            var changes = SubmissionProtectedFields.SensitiveChanges(Entry(), sub);
            Assert.Equal(4, changes.Count);
            Assert.Contains(changes, c => c.Contains("REPLACED"));
            Assert.Contains(changes, c => c.Contains("Scam"));
        }

        [Fact]
        public void SensitiveChanges_IgnoresCosmeticDifferences()
        {
            var sub = new Submission
            {
                Link = "https://XMR2CEX.com/", Email = " A@B.C ", PgpKey = Key.Replace("\n", "\r\n") + "\r\n", DirectoryStatus = DirectoryStatus.Unknown,
            };
            Assert.Empty(SubmissionProtectedFields.SensitiveChanges(Entry(), sub));
        }
    }
}
