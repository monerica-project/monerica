using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class ValidationHelperTests
    {
        [Theory]
        [InlineData("testing@example.com", true)]
        [InlineData("testing@example.com'||'", false)]
        [InlineData("testing@example.com'|||'", false)]
        [InlineData("testing@example.com'||DBMS_PIPE.RECEIVE_MESSAGE(CHR(98)||CHR(98)||CHR(98),15)||'", false)]
        public void BlocksBadEmails(string email, bool expected)
        {
            bool result = ValidationHelper.IsValidEmail(email);
            Assert.Equal(expected, result);
        }
    }
}
