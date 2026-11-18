using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Members
{
    public class MemberAccountRulesTests
    {
        [Theory]
        [InlineData("quiet_otter_4821")]
        [InlineData("Bob-99")]
        [InlineData("abc")]
        public void GoodUsernamesPass(string name) => Assert.Null(MemberAccountRules.ValidateUsername(name));

        [Theory]
        [InlineData("ab")] // too short
        [InlineData("this_name_is_far_too_long_x")] // too long
        [InlineData("has space")]
        [InlineData("émile")] // non-ASCII look-alikes are refused outright
        [InlineData("bob@example")]
        [InlineData("")]
        public void BadUsernamesFail(string name) => Assert.NotNull(MemberAccountRules.ValidateUsername(name));

        [Theory]
        [InlineData("admin")]
        [InlineData("ADMIN")]
        [InlineData("Administrator")]
        [InlineData("adm1n")]
        [InlineData("a_d_m_i_n")]
        [InlineData("site-admin-2")]
        [InlineData("moderator")]
        [InlineData("M0derat0r")]
        [InlineData("mod")]
        [InlineData("support")]
        [InlineData("5upport_team")]
        [InlineData("staff")]
        [InlineData("0ff1c1al")]
        [InlineData("pgp_verified")]
        [InlineData("deleted-member")]
        [InlineData("listing-owner")]
        [InlineData("D8054E3A")]           // looks like a PGP key id
        [InlineData("7C22-1A25-BEEF")]     // looks like a fingerprint
        public void ReservedNamesAreRefused(string name)
        {
            Assert.True(MemberAccountRules.IsReserved(name));
            Assert.NotNull(MemberAccountRules.ValidateUsername(name));
        }

        [Theory]
        [InlineData("ExampleDirectory")]
        [InlineData("example_directory")]
        [InlineData("the-example-dir")] // contains a word of the site name ("example")
        [InlineData("owner")]
        [InlineData("Owner_Example_com")]
        [InlineData("satoshi")]
        [InlineData("SAT0SHI")]
        [InlineData("satoshi_fan")]
        public void SiteNameAndAdminUsernamesCantBeImitated(string name)
        {
            var reserved = new[] { "Example Directory", "owner@example.com", "satoshi" };
            Assert.NotNull(MemberAccountRules.ValidateUsername(name, reserved));
        }

        [Fact]
        public void UnrelatedNamesPassWithTheSiteListLoaded() =>
            Assert.Null(MemberAccountRules.ValidateUsername("brave_heron_77", new[] { "Example Directory", "owner@example.com" }));

        [Theory]
        [InlineData("short")]
        [InlineData("aaaaaaaaaaaa")] // too few different characters
        [InlineData("myname_secret_words")] // contains the username
        public void WeakPasswordsFail(string password) => Assert.NotNull(MemberAccountRules.ValidatePassword(password, "myname"));

        [Fact]
        public void PasswordLengthBounds()
        {
            Assert.Null(MemberAccountRules.ValidatePassword("ten chars!"));
            Assert.NotNull(MemberAccountRules.ValidatePassword("nine char"));
            Assert.NotNull(MemberAccountRules.ValidatePassword(new string('x', 120) + "abcdefghi"));
        }

        [Fact]
        public void GeneratedCredentialsAreValidAndRandom()
        {
            var names = new HashSet<string>();
            var passwords = new HashSet<string>();
            for (var i = 0; i < 200; i++)
            {
                var u = MemberCredentialGenerator.Username();
                var p = MemberCredentialGenerator.Password();
                Assert.Null(MemberAccountRules.ValidateUsername(u));
                Assert.Null(MemberAccountRules.ValidatePassword(p, u));
                Assert.True(p.Length >= 20);
                names.Add(u);
                passwords.Add(p);
            }

            Assert.True(names.Count > 150);
            Assert.Equal(200, passwords.Count);
        }
    }
}
