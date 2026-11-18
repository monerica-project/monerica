using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>Outcome of a member sign-up.</summary>
    public sealed class MemberRegisterResult
    {
        public ApplicationUser? User { get; set; }

        public bool Succeeded => this.User != null && this.Errors.Count == 0;

        /// <summary>Form errors keyed by field name ("" = whole form).</summary>
        public Dictionary<string, string> Errors { get; } = new ();
    }
}
