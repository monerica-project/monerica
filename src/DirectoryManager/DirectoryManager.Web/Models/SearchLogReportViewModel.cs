using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Models
{
    public class SearchLogReportViewModel
    {
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public int TotalTerms { get; set; }
        public IReadOnlyList<SearchReportItem> ReportItems { get; set; } = Array.Empty<SearchReportItem>();
    }
}
