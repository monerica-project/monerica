using System;

namespace DirectoryManager.Web.Models.Submissions
{

    public class SubmissionFindingListingRowVm
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Link { get; set; }

        public string Status { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
        public string Subcategory { get; set; } = string.Empty;

        public int AgeDays { get; set; }
    }
}