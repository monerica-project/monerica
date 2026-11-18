using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Members
{
    public class MemberReplyInputModel
    {
        public const int MaxBodyLength = 4000;

        public int? ParentCommentId { get; set; }

        [StringLength(MaxBodyLength, ErrorMessage = "Your reply is too long. Please keep it under {1} characters.")]
        [Display(Name = "Your reply")]
        public string? Body { get; set; }
    }
}
