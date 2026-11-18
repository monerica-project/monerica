using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class ApplicationUser : ApplicationUserStateInfo
    {
        public ApplicationUser()
        {
        }

        [StringLength(36)]
        public override string Id
        {
            get
            {
                return base.Id;
            }

            set
            {
                base.Id = value;
            }
        }

        /// <summary>
        /// Set when a member deleted their account. The row is kept but anonymized (no password,
        /// placeholder username, locked out) so their reviews keep an author and show as
        /// "deleted member". Null for live accounts.
        /// </summary>
        public DateTime? DeletedUtc { get; set; }

        /// <summary>
        /// Set when an admin disabled (banned) a member account. A disabled account can't sign in
        /// and is signed out on its next request. Null for accounts in good standing.
        /// </summary>
        public DateTime? DisabledUtc { get; set; }

        /// <summary>
        /// Fingerprint (uppercase hex) of a PGP key the member proved they control by decrypting a
        /// one-time challenge, exactly as in the PGP review flow (same key selection: the key the
        /// challenge was encrypted to). Null when no key is linked. Unique: one key belongs to at
        /// most one account. Display only: it never grants listing-owner or moderator powers, and
        /// member posts keep their synthetic "usr:" AuthorFingerprint.
        /// </summary>
        [StringLength(64)]
        public string? PgpFingerprint { get; set; }

        /// <summary>When <see cref="PgpFingerprint"/> was proven and linked (UTC). Null when no key.</summary>
        public DateTime? PgpLinkedUtc { get; set; }

        /// <summary>
        /// Set when an admin blocked this member from posting. The member can still sign in, read
        /// and delete their own posts, but can't write or edit reviews or replies, or link a PGP
        /// key. Separate from <see cref="DisabledUtc"/> (a full ban). Null = may post.
        /// </summary>
        public DateTime? PostingBlockedUtc { get; set; }

        /// <summary>Admin username that set the posting block (admin-only).</summary>
        [StringLength(256)]
        public string? PostingBlockedBy { get; set; }

        /// <summary>Short admin-only reason for the posting block. Never shown to the member.</summary>
        [StringLength(200)]
        public string? PostingBlockReason { get; set; }

        /// <summary>When the last posting block was lifted (UTC), for the admin audit line.</summary>
        public DateTime? PostingUnblockedUtc { get; set; }

        /// <summary>Admin username that lifted the last posting block.</summary>
        [StringLength(256)]
        public string? PostingUnblockedBy { get; set; }
    }
}
