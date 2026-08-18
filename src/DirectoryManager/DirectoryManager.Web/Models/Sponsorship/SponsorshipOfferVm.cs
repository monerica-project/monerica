namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipOfferVm
    {
        public int Days { get; set; }
        public decimal PriceUsd { get; set; }
        public decimal PricePerDay { get; set; }
        public string Description { get; set; } = "";

        /// <summary>Cross-tier existing-sponsor perk percent applied to this price (0 = none).</summary>
        public decimal DiscountPercent { get; set; }

        /// <summary>Price after the existing-sponsor perk; equals <see cref="PriceUsd"/> when there is no discount.</summary>
        public decimal DiscountedPriceUsd { get; set; }

        public bool HasExistingSponsorPerk => this.DiscountPercent > 0m;
    }
}
