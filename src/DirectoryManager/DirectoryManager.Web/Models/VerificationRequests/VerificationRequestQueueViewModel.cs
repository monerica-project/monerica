using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;

namespace DirectoryManager.Web.Models.VerificationRequests
{
    public class VerificationRequestQueueViewModel
    {
        public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;

        public IReadOnlyList<VerificationRequest> Items { get; set; } = Array.Empty<VerificationRequest>();

        public int Total { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;
    }
}
