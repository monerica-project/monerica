namespace DirectoryManager.Web.Models.Reports
{
    public class AdvertiserInvoiceListViewModel
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;

        public int Page { get; set; }
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }

        public IList<AdvertiserInvoiceRow> Rows { get; set; } = new List<AdvertiserInvoiceRow>();
        public decimal TotalPaidAllTime { get; set; }

    }
}
