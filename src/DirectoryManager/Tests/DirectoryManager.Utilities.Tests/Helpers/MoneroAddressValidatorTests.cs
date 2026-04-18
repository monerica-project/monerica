using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Utilities.Tests
{
    public class MoneroAddressValidatorTests
    {
        public static IEnumerable<object[]> ValidAddressData()
        {
            yield return new object[] { "4" + new string('A', 94) };   // 95-char standard
            yield return new object[] { "8" + new string('A', 94) };   // 95-char subaddress
        }

        public static IEnumerable<object[]> InvalidAddressData()
        {
            yield return new object[] { "5" + new string('A', 94) };                    // wrong prefix
            yield return new object[] { "4" + "0" + new string('A', 93) };              // forbidden char '0'
            yield return new object[] { "4" + "O" + new string('A', 93) };              // forbidden char 'O'
            yield return new object[] { "4" + new string('A', 47) + " " + new string('A', 47) }; // whitespace
            yield return new object[] { "4" + new string('A', 10) };                    // too short
            yield return new object[] { "4" + new string('A', 200) };                   // too long
        }

        [Theory]
        [MemberData(nameof(ValidAddressData))]
        public void Valid_Format_Addresses_Return_True(string addr)
        {
            Assert.True(MoneroAddressValidator.IsValid(addr));
        }

        [Theory]
        [MemberData(nameof(InvalidAddressData))]
        public void Invalid_Format_Addresses_Return_False(string addr)
        {
            Assert.False(MoneroAddressValidator.IsValid(addr));
        }

        [Fact]
        public void Integrated_Address_Length_106_Accepted_By_Default()
        {
            var addr = "4" + new string('A', 105); // 106 total
            Assert.True(MoneroAddressValidator.IsValid(addr));
        }

        [Fact]
        public void Integrated_Address_Rejected_When_Disabled()
        {
            var addr = "4" + new string('A', 105); // 106 total
            Assert.False(MoneroAddressValidator.IsValid(addr, allowIntegrated: false));
        }

        [Fact]
        public void Subaddress_Rejected_When_Disabled()
        {
            var addr = "8" + new string('A', 94); // 95 total
            Assert.False(MoneroAddressValidator.IsValid(addr, allowIntegrated: true, allowSubaddress: false));
        }

        [Fact]
        public void Strict_Uses_Same_Semantics_For_Now()
        {
            var addr = "4" + new string('A', 94);
            Assert.True(MoneroAddressValidator.IsValidStrict(addr));
        }
    }
}