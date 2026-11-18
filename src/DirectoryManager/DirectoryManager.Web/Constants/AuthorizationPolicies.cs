namespace DirectoryManager.Web.Constants
{
    /// <summary>
    /// Authorization policy names.
    /// <para><see cref="AdminOnly"/>: every admin / management / moderation action. Requires the
    /// admin sign-in cookie AND the Admin role. A bare <c>[Authorize]</c> also resolves to this
    /// (the default policy is AdminOnly), so forgetting the policy name fails closed.</para>
    /// <para><see cref="Member"/>: the few actions a member account may use (its own account,
    /// its own reviews and replies). Members sign in with their OWN cookie scheme
    /// (<see cref="MemberAuth.Scheme"/>), never the admin one, so on every admin page and in every
    /// "is the visitor signed in" check a member is simply anonymous.</para>
    /// </summary>
    public static class AuthorizationPolicies
    {
        public const string AdminOnly = "AdminOnly";

        public const string Member = "Member";
    }
}
