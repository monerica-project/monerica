using System.Security.Claims;
using DirectoryManager.Data.Constants;
using DirectoryManager.Web.Constants;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// The one in-code "is this visitor an admin?" check. Use it wherever code used to treat any
    /// signed-in visitor as an admin (seeing removed listings, setting a submission's status,
    /// skipping draft checks, marking test checkouts, …). A plain IsAuthenticated check is not
    /// enough now that member accounts exist.
    /// </summary>
    public static class AdminAccess
    {
        public static bool IsAdmin(this ClaimsPrincipal? user) =>
            user?.Identity?.IsAuthenticated == true
            && user.IsInRole(AuthRoles.Admin)
            && !user.HasClaim(c => c.Type == MemberAuth.MemberClaim);
    }
}
