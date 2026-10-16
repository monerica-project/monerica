using System.Globalization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Forecasting;
using DirectoryManager.Web.Models.Reports;

namespace DirectoryManager.Web.Revenue
{
    // Plain-English pricing and retention guidance from what the forecast learned.
    public static class RevenueAdvice
    {
        public static List<ForecastAdvice> Build(SponsorshipForecastModel.Result r, SponsorshipIncomeAnalyzer.Result analysis)
        {
            var advice = new List<ForecastAdvice>();
            var inv = CultureInfo.InvariantCulture;
            var usd = CultureInfo.GetCultureInfo("en-US");

            var main = r.Tiers.FirstOrDefault(t => t.Type == SponsorshipType.MainSponsor);
            if (main != null && main.SoldOutPctLast90Days >= 90m)
            {
                advice.Add(new ForecastAdvice
                {
                    Kind = "positive",
                    Text = $"Main sponsor slots were sold out {main.SoldOutPctLast90Days.ToString("0", inv)}% of the last 90 days, and an open slot is " +
                           "taken quickly. Demand is above supply: this is the tier where a price increase is least likely to cost you sponsors. " +
                           "Try the 'price change per year' setting to see what an increase would add.",
                });
            }
            else if (main != null && main.Capacity != int.MaxValue && main.ActiveNow < main.Capacity)
            {
                advice.Add(new ForecastAdvice
                {
                    Kind = "info",
                    Text = $"{main.Capacity - main.ActiveNow} Main slot(s) open right now. Filling them is worth about " +
                           $"{(main.RecentDailyRate * 30.4m).ToString("C0", usd)} a month each at recent prices.",
                });
            }

            foreach (var t in r.Tiers.Where(t => t.CampaignsEnded >= 5 && t.RenewalRatePct < 35m))
            {
                advice.Add(new ForecastAdvice
                {
                    Kind = "warning",
                    Text = $"{Name(t.Type)} sponsors renew only {t.RenewalRatePct.ToString("0", inv)}% of the time. " +
                           "A reminder before expiry, or a small renewal discount, would lift the forecast more than a price change.",
                });
            }

            if (analysis.ChurnRatePct > 35m)
            {
                advice.Add(new ForecastAdvice
                {
                    Kind = "warning",
                    Text = $"{analysis.ChurnRatePct.ToString("0.#", inv)}% of advertisers are no longer live (pulled or removed). " +
                           "Better vetting up front protects future renewals.",
                });
            }

            var nextQuarter = r.Months.Skip(1).Take(3).Sum(m => m.Expected);
            var paidQuarter = r.Months.Skip(1).Take(3).Sum(m => m.Contracted);
            if (nextQuarter > 0m)
            {
                var pct = 100m * paidQuarter / nextQuarter;
                advice.Add(new ForecastAdvice
                {
                    Kind = "info",
                    Text = $"{pct.ToString("0", inv)}% of next quarter's expected revenue is already paid for. " +
                           "The rest depends on renewals and new sponsors.",
                });
            }

            if (analysis.CutShortUnearnedTotal > 0m)
            {
                advice.Add(new ForecastAdvice
                {
                    Kind = "info",
                    Text = $"{analysis.CutShortUnearnedTotal.ToString("C0", usd)} was kept from sponsorships you cut short. That money " +
                           "isn't assumed to recur.",
                });
            }

            return advice;
        }

        private static string Name(SponsorshipType t) => t switch
        {
            SponsorshipType.MainSponsor => "Main",
            SponsorshipType.CategorySponsor => "Category",
            SponsorshipType.SubcategorySponsor => "Subcategory",
            _ => t.ToString(),
        };
    }
}
