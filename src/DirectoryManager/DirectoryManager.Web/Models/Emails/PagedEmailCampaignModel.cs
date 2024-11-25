using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Web.Models.Emails
{
    public class PagedEmailCampaignModel
    {
        public IEnumerable<EmailCampaign> Campaigns { get; set; } = new List<EmailCampaign>();

        // Current page number
        public int PageIndex { get; set; }

        // Number of items per page
        public int PageSize { get; set; }

        // Total number of items across all pages
        public int TotalItems { get; set; }

        // Calculates the total number of pages
        public int TotalPages => (int)Math.Ceiling((double)this.TotalItems / this.PageSize);

        // Flag to check if there is a previous page
        public bool HasPreviousPage => this.PageIndex > 1;

        // Flag to check if there is a next page
        public bool HasNextPage => this.PageIndex < this.TotalPages;
    }
}