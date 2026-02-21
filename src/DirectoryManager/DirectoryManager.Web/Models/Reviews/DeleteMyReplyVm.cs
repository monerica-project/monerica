using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{

    public class DeleteMyReplyVm
    {
        public Guid FlowId { get; set; }
        public string Fingerprint { get; set; } = string.Empty;

        public DirectoryEntryReviewComment Comment { get; set; } = default!;
        public DirectoryEntryReview ParentReview { get; set; } = default!;
        public string ListingName { get; set; } = "Listing";
    }
}
