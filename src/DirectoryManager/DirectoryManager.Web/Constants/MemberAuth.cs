namespace DirectoryManager.Web.Constants
{
    /// <summary>Names used by the member (username + password) sign-in.</summary>
    public static class MemberAuth
    {
        /// <summary>
        /// The member cookie authentication scheme. Deliberately separate from Identity's
        /// application scheme (the admin cookie): HttpContext.User on ordinary requests is built
        /// from the admin cookie only, so a member is never "authenticated" there.
        /// </summary>
        public const string Scheme = "Member";

        public const string CookieName = ".dm.member";

        /// <summary>Claim present on every member principal (value "1").</summary>
        public const string MemberClaim = "dm_member";

        /// <summary>
        /// Master switch for new sign-ups. Code constant (not config) on purpose. Existing members
        /// can still sign in and manage their reviews when this is false.
        /// </summary>
        public const string RegisterPath = "/members/register";

        public const string SignInPath = "/members/sign-in";

        public const string AccountPath = "/members/account";

        public const string MyReviewsPath = "/members/reviews";

        public const string PgpKeyPath = "/members/pgp-key";

        public const string PgpKeyVerifyPath = "/members/pgp-key/verify";

        public const string WriteReviewPathPrefix = "/members/reviews/write/";

        public const string ReplyPathPrefix = "/members/replies/new/";

        /// <summary>
        /// Master switch for new sign-ups. A code constant (not config) on purpose. Existing members
        /// can still sign in and manage their reviews when this is false.
        /// </summary>
        public static bool RegistrationEnabled { get; } = true;
    }
}
