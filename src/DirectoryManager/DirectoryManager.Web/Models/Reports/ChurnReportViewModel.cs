using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.Reports
{

    /// <summary>
    /// View model for the churn report page.
    /// </summary>
    public sealed class ChurnReportViewModel
    {
        // Filters / window
        public DateTime WindowStartUtc { get; set; }

        public DateTime WindowEndOpenUtc { get; set; }

        public SponsorshipType? SponsorshipType { get; set; }

        public int? SubCategoryId { get; set; }

        public int? CategoryId { get; set; }

        // Dropdowns
        public List<SelectListItem> SponsorshipTypeOptions { get; set; } = new List<SelectListItem>();

        public List<SelectListItem> SubCategoryOptions { get; set; } = new List<SelectListItem>();

        public List<SelectListItem> CategoryOptions { get; set; } = new List<SelectListItem>();

        // Metrics
        public int ActiveAtStart { get; set; }

        public int ActivatedInWindow { get; set; }

        public int ChurnedInWindow { get; set; }

        public int ActiveAtEnd { get; set; }

        public int UniqueActiveInWindow { get; set; }

        /// <summary>
        /// ChurnedInWindow / ActiveAtStart (0..1).
        /// </summary>
        public decimal ChurnRate { get; set; }

        // Lists
        public List<ChurnActorRow> Activated { get; set; } = new List<ChurnActorRow>();

        public List<ChurnActorRow> Churned { get; set; } = new List<ChurnActorRow>();
    }
}
