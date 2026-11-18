using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Members
{
    public class MyMemberReviewsViewModel
    {
        public string Username { get; set; } = string.Empty;

        public List<DirectoryEntryReview> Reviews { get; set; } = new ();

        public List<DirectoryEntryReviewComment> Replies { get; set; } = new ();
    }
}
