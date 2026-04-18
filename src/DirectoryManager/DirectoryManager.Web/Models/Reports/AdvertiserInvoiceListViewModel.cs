namespace DirectoryManager.Web.Models.Reports
{
    public class AdvertiserInvoiceListViewModel
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        public decimal TotalPaidAllTime { get; set; }

        public int TotalPurchasedDaysExclusive { get; set; }
        public decimal AverageUsdPerDayAllTime { get; set; }

        public List<AdvertiserInvoiceRow> Rows { get; set; } = new ();
    }
}