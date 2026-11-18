using System.Globalization;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Models.TrustPages
{
    /// <summary>The public /incidents page.</summary>
    public sealed class IncidentsViewModel
    {
        public IncidentFilter Filter { get; init; } = new ();

        /// <summary>Open issues on this page (the newest ones when Kind = All).</summary>
        public IReadOnlyList<OpenIssue> OpenIssues { get; init; } = Array.Empty<OpenIssue>();

        public int OpenIssueTotal { get; init; }

        public PagedResult<DirectoryEntryReview> ReviewReports { get; init; } = new () { PageSize = IncidentFilter.PageSize, Page = 1 };

        public IReadOnlyDictionary<int, int> ListingPages { get; init; } = new Dictionary<int, int>();

        /// <summary>The page shown in a single-kind view (clamped).</summary>
        public int Page { get; init; } = 1;

        public int TotalPages { get; init; } = 1;
    }
}
