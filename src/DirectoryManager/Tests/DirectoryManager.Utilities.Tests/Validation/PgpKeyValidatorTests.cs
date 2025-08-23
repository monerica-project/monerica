using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Utilities.Tests.Validation
{
    public class PgpKeyValidatorTests
    {
        private const string SampleValidKey = @"-----BEGIN PGP PUBLIC KEY BLOCK-----
mDMEaGVmEhYJKwYBBAHaRw8BAQdAYQ1UNsXScY2iPfsqJCnL6XfwU5p/8l8dBXhkvxSnIjG0J1Ryb2NhZG9yIFN1cHBvcnQgPHN1cHBvcnRAdHJvY2Fkb3IuYXBwPohyBBMWCAAaBAsJCAcCFQgCFgECGQEFgmhlZhICngECmwMACgkQpmWTBuQhwHpwxgD9F8toOQaS8beJJVqv0mrDW7yyMSDgJeHpUE5gobmRhXEBAOy9wtKplrUQwdWbberJMPdtud2jgFMf2F0/WvcKDj4JuDgEaGVmEhIKKwYBBAGXVQEFAQEHQDXDpq6Q+PVL2Wd1oQQRWn6R/e5uadE0BwrIjHL13kpcAwEIB4hhBBgWCAAJBYJoZWYSApsMAAoJEKZlkwbkIcB6AW8A/2gPKdBVIWeYlIjXIZocP38ukO22Se9ztWVFQzCpbii6AQD5bFfD0oBnLLa1GCxFyYNk56UUdmEIE9axImh/9C4lDQ===TKK1
-----END PGP PUBLIC KEY BLOCK-----";

        private const string SecondValidKey = @"-----BEGIN PGP PUBLIC KEY BLOCK-----

mDMEZju1lhYJKwYBBAHaRw8BAQdAL7IaBR4o8lq2GfSJDebq6+TDbhe0yadov+wX
kNhmTKO0Dk5pbmphLkV4Y2hhbmdliJkEExYKAEEWIQRuN8ImFqSPPFRi7wLzGuuI
XjcxXwUCZju1lgIbAwUJCqE8KgULCQgHAgIiAgYVCgkICwIEFgIDAQIeBwIXgAAK
CRDzGuuIXjcxXypDAQDHZKixehxoWR9DzoAKCt0AiTGSUgysklChL1ucbvjeSQD8
DHWl7z0keCLLr2+2BrIQRt9noGeO+dBRU1rnIh73sgK4OARmO7WWEgorBgEEAZdV
AQUBAQdAN8+Q+23tXAjwxTF0bgpzFPYp9no1Kije5TktHBMbLjEDAQgHiH4EGBYK
ACYWIQRuN8ImFqSPPFRi7wLzGuuIXjcxXwUCZju1lgIbDAUJCqE8KgAKCRDzGuuI
XjcxX93sAQCMtkyMfQEw+3e7JkmduR9859LZ9puwVbPG4ZbGY4HEBQEA+Y+GlTb+ 
NS2/YMNdWztHoUWGKo+fdHNgAgFPWERd2Ac=
=SOq2
-----END PGP PUBLIC KEY BLOCK-----";

        [Fact]
        public void IsValid_WithValidPgpKey_ReturnsTrue()
        {
            Assert.True(PgpKeyValidator.IsValid(SampleValidKey));
        }

        [Fact]
        public void IsValid_WithSecondValidKey_ReturnsTrue()
        {
            Assert.True(PgpKeyValidator.IsValid(SecondValidKey));
        }

        [Fact]
        public void IsValid_WithCorruptedBase64_ReturnsFalse()
        {
            // flip a character in the Base64 payload
            var bad = SampleValidKey.Replace("mDMEaGVmEhYJ", "xDMEaGVmEhYJ");
            Assert.False(PgpKeyValidator.IsValid(bad));
        }

        [Fact]
        public void IsValid_MissingBeginHeader_ReturnsFalse()
        {
            var noBegin = SampleValidKey
                .Replace("-----BEGIN PGP PUBLIC KEY BLOCK-----\r\n", "");
            Assert.False(PgpKeyValidator.IsValid(noBegin));
        }

        [Fact]
        public void IsValid_MissingEndFooter_ReturnsFalse()
        {
            var noEnd = SampleValidKey
                .Replace("\r\n-----END PGP PUBLIC KEY BLOCK-----", "");
            Assert.False(PgpKeyValidator.IsValid(noEnd));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValid_NullOrWhitespaceInput_ReturnsFalse(string input)
        {
            Assert.False(PgpKeyValidator.IsValid(input));
        }

        [Fact]
        public void IsValid_RandomText_ReturnsFalse()
        {
            var junk = "this is not a pgp key at all";
            Assert.False(PgpKeyValidator.IsValid(junk));
        }
    }
}