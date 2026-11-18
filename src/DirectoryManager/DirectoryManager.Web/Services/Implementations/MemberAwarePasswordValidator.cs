using DirectoryManager.Data.Constants;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Identity;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Replaces Identity's password validator. Admin accounts keep the strict configured policy
    /// (12+ characters with upper, lower, digit and symbol). Member accounts get the member rule
    /// (<see cref="MemberAccountRules.ValidatePassword"/>): a length minimum with no composition
    /// rules, because the sign-up form suggests a long random password.
    /// </summary>
    public sealed class MemberAwarePasswordValidator : IPasswordValidator<ApplicationUser>
    {
        private readonly PasswordValidator<ApplicationUser> adminRules = new ();

        public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        {
            if (await IsAdminAsync(manager, user))
            {
                return await this.adminRules.ValidateAsync(manager, user, password);
            }

            var error = MemberAccountRules.ValidatePassword(password, user.UserName);
            return error == null
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError { Code = "MemberPassword", Description = error });
        }

        private static async Task<bool> IsAdminAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
        {
            if (string.IsNullOrEmpty(user.Id) || !manager.SupportsUserRole)
            {
                return false;
            }

            try
            {
                return await manager.IsInRoleAsync(user, AuthRoles.Admin);
            }
            catch (InvalidOperationException)
            {
                // The role row doesn't exist yet (fresh database): nobody is an admin.
                return false;
            }
        }
    }
}
