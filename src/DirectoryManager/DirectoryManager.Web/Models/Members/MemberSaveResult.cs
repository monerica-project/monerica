namespace DirectoryManager.Web.Models.Members
{
    /// <summary>Outcome of saving a member review or reply.</summary>
    public sealed class MemberSaveResult
    {
        public bool Succeeded => this.Errors.Count == 0 && !this.NotAllowed;

        /// <summary>The listing or review can't take posts (reviews closed, listing removed, review not approved, …).</summary>
        public bool NotAllowed { get; set; }

        /// <summary>True when the member's existing review on the listing was edited.</summary>
        public bool WasEdit { get; set; }

        /// <summary>True when the edited review had been published (its public page changes).</summary>
        public bool WasPublished { get; set; }

        public int? Id { get; set; }

        /// <summary>Form errors keyed by field name ("" = whole form).</summary>
        public Dictionary<string, string> Errors { get; } = new ();

        public static MemberSaveResult Denied() => new () { NotAllowed = true };

        public MemberSaveResult Error(string field, string message)
        {
            this.Errors[field] = message;
            return this;
        }
    }
}
