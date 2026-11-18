using Microsoft.AspNetCore.DataProtection;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Member usernames are assigned by the site, never typed by the visitor. The register page
    /// shows a random name and carries it in a signed, time-limited token; the POST accepts only a
    /// username that comes out of a valid token, so the form cannot be edited to pick a name.
    /// </summary>
    public sealed class MemberAssignedUsername
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

        private readonly ITimeLimitedDataProtector protector;

        public MemberAssignedUsername(IDataProtectionProvider provider)
        {
            this.protector = provider.CreateProtector("Members.AssignedUsername.v1").ToTimeLimitedDataProtector();
        }

        /// <summary>A fresh random username and the token that proves the site assigned it.</summary>
        public (string Username, string Token) Issue()
        {
            var name = MemberCredentialGenerator.Username();
            return (name, this.protector.Protect(name, Lifetime));
        }

        /// <summary>The token for a given name (used when the same name is shown again after a form error).</summary>
        public string TokenFor(string username) => this.protector.Protect(username, Lifetime);

        /// <summary>The assigned username, or null when the token is missing, altered or expired.</summary>
        public string? Read(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            {
                return null;
            }

            try
            {
                var name = this.protector.Unprotect(token);
                return MemberAccountRules.ValidateUsername(name) is null ? name : null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
    }
}
