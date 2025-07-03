using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DirectoryManager.Web.Models.Reports
{
    public class AdvertiserBreakdownViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SponsorshipType? SponsorshipType { get; set; }
        public List<SelectListItem> SponsorshipTypeOptions { get; set; } = [];
        public List<AdvertiserBreakdownRow> Rows { get; set; } = [];
    }
}
