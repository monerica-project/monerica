using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.VerificationRequests;

namespace DirectoryManager.Web.Models.VerificationRequests
{
    /// <summary>A single verification request with its listing, for the full-text detail page.</summary>
    public class VerificationRequestDetailViewModel
    {
        public VerificationRequest Request { get; set; } = null!;

        public DirectoryEntry? Entry { get; set; }
    }
}
