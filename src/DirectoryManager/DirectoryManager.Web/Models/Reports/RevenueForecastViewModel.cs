using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Revenue;

namespace DirectoryManager.Web.Models.Reports
{
    public class RevenueForecastViewModel
    {
        public int HorizonMonths { get; init; }

        public int ConfidencePercent { get; init; }

        public SponsorshipForecastModel.Result Result { get; init; } = new ();

        public List<RevenueMonth> History { get; init; } = new ();

        public decimal PriceGrowthPercent { get; init; }

        public List<ForecastAdvice> Advice { get; init; } = new ();

        /// <summary>The stream shown (null = all revenue).</summary>
        public DirectoryManager.Data.Enums.RevenueSource? Stream { get; init; }

        /// <summary>Years of history the model learns from.</summary>
        public int LookbackYears { get; init; } = 1;

        /// <summary>Years of payment history there are (the most the look-back can be).</summary>
        public int MaxLookbackYears { get; init; } = 1;

        /// <summary>Date of the first payment of any kind.</summary>
        public DateTime? FirstPaymentUtc { get; init; }
    }
}
