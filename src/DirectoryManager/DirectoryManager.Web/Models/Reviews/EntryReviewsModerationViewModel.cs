using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    public class EntryReviewsModerationViewModel
    {
        public int DirectoryEntryId { get; set; }

        public string DirectoryEntryName { get; set; } = string.Empty;

        public string DirectoryEntryKey { get; set; } = string.Empty;

        public ReviewModerationStatus? Status { get; set; }

        public IReadOnlyList<DirectoryEntryReview> Reviews { get; set; } = new List<DirectoryEntryReview>();

        public int Total { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;

        public int LastPage => Math.Max(1, (int)Math.Ceiling(this.Total / (double)this.PageSize));
    }
}
