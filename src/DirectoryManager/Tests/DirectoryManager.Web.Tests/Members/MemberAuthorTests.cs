using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Members
{
    public class MemberAuthorTests
    {
        private const string SomeFingerprint = "D8054E3A0F1B2C3D4E5F60718293A4B5C6D7E8F9";

        [Fact]
        public void AuthorKeyContainsNoHexDigits_SoItNeverMatchesAPgpFingerprint()
        {
            for (var i = 0; i < 500; i++)
            {
                var id = Guid.NewGuid().ToString();
                var key = MemberAuthor.AuthorKey(id);

                Assert.StartsWith(MemberAuthor.KeyPrefix, key);
                Assert.True(key.Length <= 40, "must fit DirectoryEntryReviews.AuthorFingerprint (40)");
                Assert.Equal(string.Empty, PgpFingerprintTools.Normalize(key));
                Assert.False(PgpFingerprintTools.Matches(key, SomeFingerprint));
                Assert.False(PgpFingerprintTools.Matches(SomeFingerprint, key));
                Assert.False(PgpFingerprintTools.Matches(key, SomeFingerprint[^16..]));
            }
        }

        [Fact]
        public void AuthorKeyIsStableAndDistinctPerAccount()
        {
            var a = Guid.NewGuid().ToString();
            var b = Guid.NewGuid().ToString();
            Assert.Equal(MemberAuthor.AuthorKey(a), MemberAuthor.AuthorKey(a.ToUpperInvariant()));
            Assert.NotEqual(MemberAuthor.AuthorKey(a), MemberAuthor.AuthorKey(b));
        }

        [Fact]
        public void DisplayNamesAndAdminText()
        {
            Assert.Equal("quiet_otter", MemberAuthor.PublicName("quiet_otter"));
            Assert.Equal(MemberAuthor.DeletedMemberName, MemberAuthor.PublicName(null));
            Assert.True(MemberAuthor.IsMember("abc"));
            Assert.False(MemberAuthor.IsMember(null));
            Assert.Equal("Member: quiet_otter", MemberAuthor.ForAdmin("usr:x", "id-1", "quiet_otter"));
            Assert.Equal("PGP: " + SomeFingerprint, MemberAuthor.ForAdmin(SomeFingerprint, null, null));
        }
    }
}
