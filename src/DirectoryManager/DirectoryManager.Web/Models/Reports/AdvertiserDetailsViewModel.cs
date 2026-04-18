namespace DirectoryManager.Web.Models.Reports
{
    public class AdvertiserDetailsViewModel
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; }
        public List<InvoiceSummaryViewModel> Invoices { get; set; } = new List<InvoiceSummaryViewModel>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
