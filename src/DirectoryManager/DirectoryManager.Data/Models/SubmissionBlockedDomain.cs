using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// A website that no link in a public submission may point to (new listings and edits).
    /// Blocking a domain also blocks every subdomain of it, so <c>muse.ai</c> also blocks
    /// <c>files.muse.ai</c>. Stored lower case without scheme, path or "www.".
    /// </summary>
    public class SubmissionBlockedDomain : CreatedStateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubmissionBlockedDomainId { get; set; }

        [StringLength(253)]
        [Display(Name = "Blocked domain", Prompt = "muse.ai")]
        required public string Domain { get; set; }

        /// <summary>Optional message shown to the submitter instead of the generic one.</summary>
        [StringLength(500)]
        [Display(Name = "Message to submitter (optional)")]
        public string? Message { get; set; }

        /// <summary>Turn a domain off without deleting it.</summary>
        [Display(Name = "Enabled")]
        public bool IsEnabled { get; set; } = true;
    }
}
