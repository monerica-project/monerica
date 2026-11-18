namespace DirectoryManager.Data.Constants
{
    /// <summary>
    /// Identity role names. Only site administrators hold a role. Member accounts (people who sign
    /// up with a username and password to write reviews) never hold one.
    /// </summary>
    public static class AuthRoles
    {
        public const string Admin = "Admin";

        /// <summary>Upper-case form Identity stores in AspNetRoles.NormalizedName.</summary>
        public const string AdminNormalized = "ADMIN";

        /// <summary>
        /// Fixed id, so the migration that creates the role and any later "make sure the role
        /// exists" code agree on the same row.
        /// </summary>
        public const string AdminRoleId = "9b2f6a4e-3c71-4d08-8e5a-7f1c2d9e0b63";
    }
}
