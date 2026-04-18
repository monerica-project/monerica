namespace DirectoryManager.Web.Models.Sponsorship;

public class BtcPayNoJsInvoiceViewModel
{
    /// <summary>Our internal invoice GUID (used for success redirect and status polling).</summary>
    public Guid OrderId { get; set; }

    /// <summary>BTCPay's invoice ID (e.g. "Axxxxxxxxxxxxxx").</summary>
    public string ProcessorInvoiceId { get; set; } = string.Empty;

    /// <summary>XMR destination address.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Exact XMR amount due as a string (e.g. "0.05823410").</summary>
    public string AmountDue { get; set; } = string.Empty;

    /// <summary>Full monero: URI suitable for encoding into a QR code.</summary>
    public string PaymentUri { get; set; } = string.Empty;

    /// <summary>BTCPay invoice status: New, Processing, Settled, Expired, Invalid.</summary>
    public string Status { get; set; } = "New";

    public bool IsPaid => this.Status is "Settled" or "Complete";
    public bool IsExpired => this.Status is "Expired" or "Invalid";
    public bool IsPending => this.Status is "Processing";

    /// <summary>Transaction ID if a payment has been detected (may be empty before mempool detection).</summary>
    public string? TxId { get; set; }

    /// <summary>UTC expiry time of the BTCPay invoice.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Seconds until the invoice expires (for display only).</summary>
    public int SecondsRemaining =>
        this.ExpiresAt.HasValue ? Math.Max(0, (int)(this.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds) : 0;

    public string FormattedTimeRemaining
    {
        get
        {
            var s = this.SecondsRemaining;
            return s <= 0 ? "Expired" : $"{s / 60}:{s % 60:D2}";
        }
    }

    /// <summary>Human-readable description of what was purchased.</summary>
    public string InvoiceDescription { get; set; } = string.Empty;
    public string ListingName { get; set; } = string.Empty;
}
