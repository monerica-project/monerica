using System;

namespace DirectoryManager.Web.Models.Submissions
{
    public class SubmissionFindingListingVm
    {
        public string? Query { get; set; }
        public bool HasSearched { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;

        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;

        public SubmissionFindingListingRowVm[] Results { get; set; } = Array.Empty<SubmissionFindingListingRowVm>();
    }
}