using System.ComponentModel.DataAnnotations;
using DirectoryManager.Web.ModelBinding;

namespace DirectoryManager.Web.Models.Members
{
    /// <summary>The member review form: rating, text, and the same order-proof fields as the PGP form.</summary>
    public class MemberReviewInputModel
    {
        public const int MaxBodyLength = 10000;

        [Range(1, 5, ErrorMessage = "Please choose a rating from 1 to 5.")]
        [Display(Name = "Rating")]
        public byte? Rating { get; set; }

        [StringLength(MaxBodyLength, ErrorMessage = "Your review is too long. Please keep it under {1} characters.")]
        [CleanMultiLine]
        [Display(Name = "Your review")]
        public string? Body { get; set; }

        [MaxLength(2048)]
        [Display(Name = "Order proof URL")]
        public string? OrderProof { get; set; }

        [MaxLength(2048)]
        [Display(Name = "Verification details")]
        public string? OrderProofContext { get; set; }
    }
}
