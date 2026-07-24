using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// A phrase that is not allowed in a public submission (name, description, note).
    /// Used to stop submissions that advertise things directory policy forbids, e.g.
    /// "no AML" or "no KYC/AML".
    ///
    /// Matching is case-insensitive and whitespace/punctuation tolerant. A term may
    /// contain <c>*</c> as a wildcard for "anything in between", so a single term
    /// <c>no*aml</c> catches "no AML", "no KYC/AML" and "no kyc or aml".
    /// </summary>
    public class SubmissionBlockedTerm : CreatedStateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubmissionBlockedTermId { get; set; }

        [StringLength(200)]
        [Display(Name = "Blocked term", Prompt = "no*aml")]
        required public string Term { get; set; }

        /// <summary>Optional message shown to the submitter instead of the generic one.</summary>
        [StringLength(500)]
        [Display(Name = "Message to submitter (optional)")]
        public string? Message { get; set; }

        /// <summary>Turn a term off without deleting it.</summary>
        [Display(Name = "Enabled")]
        public bool IsEnabled { get; set; } = true;
    }
}
