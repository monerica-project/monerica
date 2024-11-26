using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Utilities.Helpers
{
    public class InputHelper
    {
        public static string SetEmail(string? email)
        {
            var emailAttribute = new EmailAddressAttribute();

            return (email != null && emailAttribute.IsValid(email)) ? email.Trim() : string.Empty;
        }
    }
}