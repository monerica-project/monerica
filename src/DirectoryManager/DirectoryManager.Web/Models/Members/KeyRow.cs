namespace DirectoryManager.Web.Models.Members
{
    public sealed class KeyRow
    {
        public string Fingerprint { get; set; } = string.Empty;

        public DateTime? FirstSeenUtc { get; set; }

        public DateTime? LastSeenUtc { get; set; }

        public PostStatusCounts Reviews { get; set; } = new ();

        public PostStatusCounts Replies { get; set; } = new ();

        public int ListingCount { get; set; }

        public bool IsModerator { get; set; }

        public bool IsBlocked { get; set; }

        public List<OwnedListing> OwnerOf { get; set; } = new ();

        public LinkedMember? LinkedMember { get; set; }

        public string Short => ShortFingerprint(this.Fingerprint);

        public static string ShortFingerprint(string? fp) =>
            string.IsNullOrEmpty(fp) ? string.Empty : fp.Length > 16 ? fp[^16..] : fp;
    }
}
