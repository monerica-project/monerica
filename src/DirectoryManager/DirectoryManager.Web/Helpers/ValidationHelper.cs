using System.Globalization;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    public class ValidationHelper
    {
        public const int TimeOutMilliseconds = 250;

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            email = email.Trim();

            if (email.EndsWith("'"))
            {
                return false;
            }

            try
            {
                // Normalize the domain
                email = Regex.Replace(
                            email,
                            @"(@)(.+)$",
                            DomainMapper,
                            RegexOptions.None,
                            TimeSpan.FromMilliseconds(TimeOutMilliseconds));

                // Examines the domain part of the email and normalizes it.
                string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    string domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(
                    email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(TimeOutMilliseconds));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
