using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>The facts <see cref="SeoDescriptionBuilder.ForListing"/> summarizes for one listing.</summary>
    public sealed class ListingDescriptionFacts
    {
        public string Name { get; set; } = string.Empty;

        public string? SubcategoryName { get; set; }

        public string? CurrencyName { get; set; }

        public DirectoryStatus Status { get; set; }

        public KycPolicy? KycPolicy { get; set; }

        public Liquidity Liquidity { get; set; }

        public double? AverageRating { get; set; }

        public int ReviewCount { get; set; }

        /// <summary>True when the listing has a second link (the Tor address, by config).</summary>
        public bool HasLink2 { get; set; }

        /// <summary>True when the listing has a third link (the I2P address, by config).</summary>
        public bool HasLink3 { get; set; }

        /// <summary>The configured label of the second link ("Tor").</summary>
        public string? Link2Name { get; set; }

        /// <summary>The configured label of the third link ("I2P").</summary>
        public string? Link3Name { get; set; }

        public string? Description { get; set; }
    }
}
