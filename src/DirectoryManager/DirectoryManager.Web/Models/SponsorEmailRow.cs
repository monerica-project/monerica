public class SponsorEmailRow
{
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Name of the listing tied to the most recent invoice for this email.
    /// </summary>
    public string ListingName { get; set; } = string.Empty;

    public int InvoiceCount { get; set; }

    public int PaidInvoiceCount { get; set; }

    /// <summary>
    /// Sum of the requested amount across this email's paid invoices.
    /// </summary>
    public decimal TotalPaid { get; set; }

    public DateTime LastInvoiceDate { get; set; }

    public DateTime LastCampaignEndDate { get; set; }
}