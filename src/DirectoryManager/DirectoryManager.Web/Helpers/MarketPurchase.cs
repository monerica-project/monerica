using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>One paid sponsorship: the day it was bought, its list price per day, and the length bought.</summary>
    public sealed record MarketPurchase(DateTime Day, decimal ListPerDay, int Days, SponsorshipType Type)
    {
        /// <summary>Monthly purchases are the like-for-like comparison with today's 30-day price.</summary>
        public bool IsMonthly => this.Days <= 31;
    }
}
