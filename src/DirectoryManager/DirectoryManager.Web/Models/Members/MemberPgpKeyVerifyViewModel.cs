namespace DirectoryManager.Web.Models.Members
{
    /// <summary>Step 2 of linking a key: the encrypted one-time code and the key it was encrypted to.</summary>
    public class MemberPgpKeyVerifyViewModel
    {
        public string Fingerprint { get; set; } = string.Empty;

        public string Ciphertext { get; set; } = string.Empty;

        public DateTime ExpiresUtc { get; set; }

        /// <summary>True when this will replace a key that is already linked.</summary>
        public bool IsReplace { get; set; }
    }
}
