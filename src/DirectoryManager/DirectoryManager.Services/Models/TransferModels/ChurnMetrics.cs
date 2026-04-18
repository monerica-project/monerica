// ChurnMetrics.cs
using System;
using System.Collections.Generic;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Services.Models.TransferModels
{
    /// <summary>
    /// Aggregated churn metrics for a given time window and optional scope.
    /// </summary>
    public sealed class ChurnMetrics
    {
        /// <summary>
        /// Gets or sets the inclusive window start (UTC).
        /// </summary>
        public DateTime WindowStartUtc { get; set; }

        /// <summary>
        /// Gets or sets the exclusive window end (UTC).
        /// </summary>
        public DateTime WindowEndOpenUtc { get; set; }

        /// <summary>
        /// Gets or sets the optional sponsorship type filter used to compute this result.
        /// </summary>
        public SponsorshipType? SponsorshipType { get; set; }

        /// <summary>
        /// Gets or sets the optional subcategory id filter used to compute this result.
        /// </summary>
        public int? SubCategoryId { get; set; }

        /// <summary>
        /// Gets or sets the optional category id filter used to compute this result.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers active at <see cref="WindowStartUtc"/>.
        /// </summary>
        public int ActiveAtStart { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers whose first active day
        /// falls within the window (i.e., they were not active at the window start).
        /// </summary>
        public int ActivatedInWindow { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers that ceased to be active
        /// within the window (i.e., their last active day falls inside the window).
        /// </summary>
        public int ChurnedInWindow { get; set; }

        /// <summary>
        /// Gets or sets the number of unique advertisers active at the end of the window.
        /// Typically: ActiveAtStart + ActivatedInWindow - ChurnedInWindow (bounded at zero).
        /// </summary>
        public int ActiveAtEnd { get; set; }

        /// <summary>
        /// Gets or sets the distinct number of advertisers who were active at any time
        /// during the window (union of active-at-start and those activated during the window).
        /// </summary>
        public int UniqueActiveInWindow { get; set; }

        /// <summary>
        /// Gets or sets the churn rate for the window. Commonly calculated as
        /// ChurnedInWindow / max(ActiveAtStart, 1).
        /// </summary>
        public decimal ChurnRate { get; set; }

        /// <summary>
        /// Gets or sets the identifiers of advertisers that churned in the window.
        /// </summary>
        public List<int> ChurnedDirectoryEntryIds { get; set; } = new List<int>();

        /// <summary>
        /// Gets or sets the identifiers of advertisers that activated in the window.
        /// </summary>
        public List<int> ActivatedDirectoryEntryIds { get; set; } = new List<int>();
        public int GrossChurnInWindow { get; set; }
    }
}
