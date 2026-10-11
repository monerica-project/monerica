namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Pure pricing maths for a video-sponsorship order: unit price x number of videos, minus the
    /// Existing Sponsor Perk percent. Single source of truth for the price shown and the price
    /// charged, so the two can never diverge.
    /// </summary>
    public static class VideoSponsorPricing
    {
        public static (decimal Original, decimal DiscountAmount, decimal Total) Compute(
            decimal unitPrice, int quantity, decimal discountPercent)
        {
            if (unitPrice < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(unitPrice));
            }

            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            var original = decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
            var total = SponsorshipDiscountHelper.ApplyPercentDiscount(original, discountPercent);
            return (original, original - total, total);
        }

        /// <summary>
        /// Resolves the buyer's answer to "does demonstrating your product cost more than $100?".
        /// "No" is always 0. "Yes" needs an amount above the threshold (and below a sanity cap);
        /// that amount is then charged in full on top of the sponsorship price, never discounted.
        /// </summary>
        public static bool TryResolveDemoCost(bool costsOverThreshold, string? rawAmount, out decimal demoCost, out string? error)
        {
            demoCost = 0m;
            error = null;

            if (!costsOverThreshold)
            {
                return true;
            }

            var threshold = DirectoryManager.Common.Constants.IntegerConstants.VideoSponsorDemoCostThresholdUsd;
            var max = DirectoryManager.Common.Constants.IntegerConstants.VideoSponsorDemoCostMaxUsd;
            var cleaned = (rawAmount ?? string.Empty).Replace("$", string.Empty).Replace(",", string.Empty).Trim();

            if (!decimal.TryParse(
                    cleaned,
                    System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed))
            {
                error = $"Enter the demo cost in US dollars (more than ${threshold}).";
                return false;
            }

            parsed = decimal.Round(parsed, 2, MidpointRounding.AwayFromZero);
            if (parsed <= threshold)
            {
                error = $"The demo cost must be more than ${threshold}. If it is ${threshold} or less, choose No.";
                return false;
            }

            if (parsed > max)
            {
                error = $"The demo cost can be at most ${max:N0}. Contact us for anything larger.";
                return false;
            }

            demoCost = parsed;
            return true;
        }

        /// <summary>
        /// Splits a total into per-video amounts that add back up to exactly the total
        /// (any leftover cents go on the first videos).
        /// </summary>
        public static List<decimal> SplitAcrossItems(decimal total, int quantity)
        {
            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            var totalCents = (long)decimal.Round(total * 100m, 0, MidpointRounding.AwayFromZero);
            var baseCents = totalCents / quantity;
            var remainder = totalCents % quantity;

            var parts = new List<decimal>(quantity);
            for (var i = 0; i < quantity; i++)
            {
                parts.Add((baseCents + (i < remainder ? 1 : 0)) / 100m);
            }

            return parts;
        }
    }
}
