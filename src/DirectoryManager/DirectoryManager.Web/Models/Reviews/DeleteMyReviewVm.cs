using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{


    public class DeleteMyReviewVm
    {
        public Guid FlowId { get; set; }
        public string Fingerprint { get; set; } = string.Empty;

        public DirectoryEntryReview Review { get; set; } = default!;
        public string ListingName { get; set; } = "Listing";
    }
}
