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
    }
}
