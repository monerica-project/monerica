using System.Globalization;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class InvoicePlotting
    {
        private const string Culture = StringConstants.EnglishUS;

        public byte[] CreateMonthlyAvgPerListingDailyPriceChart(
    IEnumerable<SponsoredListingInvoice> invoices,
    Currency displayCurrency,
    DateTime rangeStart,
    DateTime rangeEnd,
    string? filterLabel = null)
        {
            var paid = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();
            if (!paid.Any())
                return Array.Empty<byte>();

            // Build inclusive month list for [rangeStart, rangeEnd]
            var firstMonth = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            var lastMonth = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);

            var months = new List<DateTime>();
            for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
                months.Add(m);

            // Helper: inclusive day count (>=1 for valid ranges)
            static int InclusiveDays(DateTime start, DateTime end)
            {
                var s = start.Date;
                var eOpen = end.Date.AddDays(1);
                var days = (int)(eOpen - s).TotalDays;
                return Math.Max(0, days);
            }

            // For each month:
            //   - compute each invoice's per-day rate: amount / total_campaign_days
            //   - weight by the number of overlap days WITHIN that month
            //   - Month's metric = (sum(rate * overlapDays)) / (sum(overlapDays))
            //     i.e., a weighted average of per-day price across all active listing-days
            var monthData = months.Select(m =>
            {
                var ms = m;
                var me = m.AddMonths(1).AddDays(-1);

                decimal weightedSum = 0m;
                long weightDays = 0;

                foreach (var inv in paid)
                {
                    var amt = inv.AmountIn(displayCurrency);
                    if (amt <= 0m)
                        continue;

                    var s = inv.CampaignStartDate.Date;
                    var e = inv.CampaignEndDate.Date;
                    if (e < s)
                        continue;

                    var totalCampaignDays = InclusiveDays(s, e);
                    if (totalCampaignDays <= 0)
                        continue;

                    // overlap with this month (inclusive)
                    var os = s > ms ? s : ms;
                    var oe = e < me ? e : me;
                    if (oe < os)
                        continue;

                    var overlapDays = InclusiveDays(os, oe);
                    if (overlapDays <= 0)
                        continue;

                    var perDay = amt / totalCampaignDays; // daily price this listing paid
                    weightedSum += perDay * overlapDays;
                    weightDays += overlapDays;
                }

                decimal avgPerListingPerDay = weightDays > 0 ? (weightedSum / weightDays) : 0m;
                return new { Month = m, AvgPerListingPerDay = avgPerListingPerDay, SampleListingDays = weightDays };
            }).ToList();

            if (monthData.All(d => d.AvgPerListingPerDay <= 0m))
                return Array.Empty<byte>();

            // Bars (you can swap to a line if you prefer)
            var bars = monthData.Select((d, idx) => new Bar { Position = idx, Value = (double)d.AvgPerListingPerDay }).ToList();

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, months);
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);
            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + Math.Max(yOffset * 1.15, 0.002));
            if (lim.Bottom != 0 || lim.Top < neededTop)
                plt.Axes.SetLimitsY(0, neededTop);

            string ValueLabel(decimal v)
            {
                if (displayCurrency == Currency.USD)
                    return v.ToString("C", CultureInfo.CreateSpecificCulture(Culture));
                // Non-USD: print compact decimals
                return v >= 1m ? v.ToString("0.000")
                     : v >= 0.1m ? v.ToString("0.0000")
                     : v >= 0.01m ? v.ToString("0.00000")
                     : v >= 0.001m ? v.ToString("0.000000")
                     : v.ToString("0.0000000").TrimEnd('0').TrimEnd('.');
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                var txt = plt.Add.Text(ValueLabel((decimal)y), x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Avg Daily Price per Listing ({unit}/day)");
            plt.XLabel("Month");
            plt.YLabel($"{unit} per listing per day");

            // Show filter + sample size in subtitle for context
            if (!string.IsNullOrWhiteSpace(filterLabel))
            {
                var totalListingDays = monthData.Sum(d => d.SampleListingDays);
                var subtitle = $"{filterLabel} — Weighted by listing-days (total sample: {totalListingDays:n0})";
                AddSubtitleBelowTitle(plt, subtitle);
            }

            return plt.GetImageBytes(1200, 600, ImageFormat.Png);
        }



        public byte[] CreateMonthlyIncomeBarChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            string? filterLabel = null)
        {
            var list = invoices?.ToList() ?? new ();
            if (list.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var grouped = list
                .GroupBy(i => new DateTime(i.CreateDate.Year, i.CreateDate.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new { Month = g.Key, Total = g.Sum(inv => inv.AmountIn(displayCurrency)) })
                .Where(x => x.Total > 0m)
                .ToList();

            if (grouped.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var bars = grouped.Select((d, idx) => new Bar { Position = idx, Value = (double)d.Total }).ToList();

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, grouped.Select(g => g.Month).ToList());
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);

            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + (yOffset * 1.15));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                string label = displayCurrency == Currency.USD
                    ? ((decimal)y).ToString("C0", CultureInfo.CreateSpecificCulture(Culture))
                    : $"{(decimal)y:0.######}";
                var txt = plt.Add.Text(label, x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            AddSubtitleBelowTitle(plt, filterLabel);

            return plt.GetImageBytes(1200, 800, ImageFormat.Png);
        }

        public byte[] CreateMonthlyAvgDailyRevenueChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            string? filterLabel = null)
        {
            var paid = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();
            if (!paid.Any())
            {
                return Array.Empty<byte>();
            }

            var firstMonth = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            var lastMonth = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);

            var months = new List<DateTime>();
            for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            var data = months.Select(m =>
            {
                int daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
                var ms = m;
                var me = m.AddMonths(1).AddDays(-1);
                decimal total = 0m;

                foreach (var inv in paid)
                {
                    var amt = inv.AmountIn(displayCurrency);
                    if (amt <= 0m)
                    {
                        continue;
                    }

                    var s = inv.CampaignStartDate.Date;
                    var e = inv.CampaignEndDate.Date;
                    if (e < s)
                    {
                        continue;
                    }

                    // inclusive campaign days (matches your other code)
                    var spanDays = (decimal)((e - s).TotalDays + 1);
                    if (spanDays <= 0)
                    {
                        continue;
                    }

                    // overlap with the month
                    var os = s > ms ? s : ms;
                    var oe = e < me ? e : me;
                    if (oe < os)
                    {
                        continue;
                    }

                    var overlapDays = (decimal)((oe - os).TotalDays + 1);
                    total += (amt / spanDays) * overlapDays;
                }

                return new { Month = m, AvgPerDay = daysInMonth > 0 ? total / daysInMonth : 0m };
            }).ToList();

            if (data.All(d => d.AvgPerDay <= 0m))
            {
                return Array.Empty<byte>();
            }

            var now = DateTime.UtcNow;

            var bars = data.Select((d, idx) => new Bar
            {
                Position = idx,
                Value = (double)d.AvgPerDay,
                FillColor = (d.Month.Year == now.Year && d.Month.Month == now.Month)
                    ? Color.FromHex("#000000")
                    : Color.FromHex("#dddddd"),
            }).ToList();

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, months);
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);
            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + Math.Max(yOffset * 1.15, 0.002));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            string ValueLabel(decimal v)
            {
                if (displayCurrency == Currency.USD)
                {
                    return v.ToString("C", CultureInfo.CreateSpecificCulture(Culture));
                }

                // Longer format for any non-USD currency (one extra decimal place)
                return v >= 1m ? v.ToString("0.000")
                     : v >= 0.1m ? v.ToString("0.0000")
                     : v >= 0.01m ? v.ToString("0.00000")
                     : v >= 0.001m ? v.ToString("0.000000")
                     : v.ToString("0.0000000").TrimEnd('0').TrimEnd('.');
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                var txt = plt.Add.Text(ValueLabel((decimal)y), x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Average Daily Revenue ({unit}/day)");
            plt.XLabel("Month");
            plt.YLabel($"{unit} per day");

            plt.Title($"Average Daily Revenue ({unit}/day)");
            plt.XLabel("Month");
            plt.YLabel($"{unit} per day");

            AddSubtitleBelowTitle(plt, filterLabel);

            return plt.GetImageBytes(1200, 600, ImageFormat.Png);
        }

        public byte[] CreateMonthlyIncomeBarChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            string? filterLabel = null)
        {
            var list = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();
            if (list.Count == 0)
            {
                return Array.Empty<byte>();
            }

            // Build inclusive month list for [rangeStart, rangeEnd]
            var firstMonth = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            var lastMonth = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);
            var months = new List<DateTime>();
            for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            // Accrue each invoice’s amount across overlap days per month
            var monthlyTotals = new List<decimal>(months.Count);
            foreach (var m in months)
            {
                var ms = m;
                var me = m.AddMonths(1).AddDays(-1);
                decimal total = 0m;

                foreach (var inv in list)
                {
                    var amt = inv.AmountIn(displayCurrency);
                    if (amt <= 0m)
                    {
                        continue;
                    }

                    var s = inv.CampaignStartDate.Date;
                    var e = inv.CampaignEndDate.Date;
                    if (e < s)
                    {
                        continue;
                    }

                    var spanDays = (decimal)((e - s).TotalDays + 1);
                    if (spanDays <= 0)
                    {
                        continue;
                    }

                    var os = s > ms ? s : ms;
                    var oe = e < me ? e : me;
                    if (oe < os)
                    {
                        continue;
                    }

                    var overlapDays = (decimal)((oe - os).TotalDays + 1);
                    total += (amt / spanDays) * overlapDays;
                }

                monthlyTotals.Add(total);
            }

            // Plot
            var bars = monthlyTotals.Select((v, i) => new Bar { Position = i, Value = (double)v }).ToList();
            if (bars.All(b => b.Value == 0))
            {
                return Array.Empty<byte>();
            }

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, months);
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);
            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + (yOffset * 1.15));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                string label = displayCurrency == Currency.USD
                    ? ((decimal)y).ToString("C0", CultureInfo.CreateSpecificCulture(Culture))
                    : $"{(decimal)y:0.######}";
                var txt = plt.Add.Text(label, x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            AddSubtitleBelowTitle(plt, filterLabel);

            return plt.GetImageBytes(1200, 800, ImageFormat.Png);
        }

        public byte[] CreateSubcategoryRevenuePieChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            IDictionary<int, string> categoryNames,
            IDictionary<int, string> subcategoryNames,
            IDictionary<int, int> subcategoryToCategory,
            Currency displayCurrency)
        {
            var list = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();

            var breakdown = list
                .Where(i => i.SubCategoryId.HasValue)
                .GroupBy(i => i.SubCategoryId!.Value)
                .Select(g =>
                {
                    int subId = g.Key;
                    subcategoryToCategory.TryGetValue(subId, out var catId);
                    categoryNames.TryGetValue(catId, out var catLabel);
                    subcategoryNames.TryGetValue(subId, out var subLabel);

                    string label = $"{catLabel ?? $"(Unknown Cat {catId})"} > {subLabel ?? $"(Unknown Sub {subId})"}";
                    decimal total = g.Sum(i => i.AmountIn(displayCurrency));
                    return (subId, label, total);
                })
                .Where(x => x.total > 0m)
                .OrderByDescending(x => x.total)
                .ToArray();

            if (breakdown.Length == 0)
            {
                return Array.Empty<byte>();
            }

            double grand = (double)breakdown.Sum(x => x.total);

            // palette (unchanged)
            var hexPalette = new[]
            {
                // D3 Category20
                "#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd",
                "#8c564b", "#e377c2", "#7f7f7f", "#bcbd22", "#17becf",
                "#393b79", "#637939", "#8c6d31", "#843c39", "#7b4173",
                "#3182bd", "#31a354", "#756bb1", "#636363", "#e6550d",

                // D3 Category20b
                "#393b79", "#5254a3", "#6b6ecf", "#9c9ede", "#637939",
                "#8ca252", "#b5cf6b", "#cedb9c", "#8c6d31", "#bd9e39",
                "#e7ba52", "#e7cb94", "#843c39", "#ad494a", "#d6616b",
                "#e7969c", "#7b4173", "#a55194", "#ce6dbd", "#de9ed6"
            };
            var palette = hexPalette.Select(Color.FromHex).ToArray();

            var slices = breakdown.Select((row, idx) =>
            {
                double val = (double)row.total;
                double pct = grand > 0 ? Math.Round(val * 100.0 / grand, 1) : 0;
                var slice = new PieSlice(val, palette[idx % palette.Length], $"{pct}%");
                slice.LegendText = $"{row.label} — {FormatValue((decimal)val, displayCurrency)}";
                return slice;
            }).ToArray();

            var plt = new Plot();
            plt.HideAxesAndGrid();

            var pie = plt.Add.Pie(slices);
            pie.DonutFraction = 0;
            pie.SliceLabelDistance = 1.2;
            pie.Rotation = Angle.FromDegrees(-90);

            plt.Title($"Revenue by Subcategory ({AxisUnitLabel(displayCurrency)})");
            plt.ShowLegend(Edge.Right);

            return plt.GetImageBytes(800, 600, ImageFormat.Png);
        }

        private static string FormatValue(decimal v, Currency currency)
        {
            if (currency == Currency.USD)
            {
                return v.ToString("C0", CultureInfo.CreateSpecificCulture(Culture));
            }

            return $"{v:0.######} {currency}";
        }

        private static string AxisUnitLabel(Currency currency) =>
            currency == Currency.USD ? "USD" : currency.ToString();

        private static void ApplyMonthCategoryTicks(ScottPlot.Plot plt, IReadOnlyList<DateTime> months)
        {
            double[] ticks = Enumerable.Range(0, months.Count).Select(i => (double)i).ToArray();
            string[] labels = months.Select(m => $"{m:MMM}\n{m:yyyy}").ToArray(); // two-line labels

            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks, labels);

            // Keep axis text horizontal (multi-line), which reduces height dramatically vs 90° rotation
            plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        }

        private static void PadXAxisForBars(ScottPlot.Plot plt, int barCount, double rightPad = 1.0)
        {
            double left = -0.5;
            double right = (barCount - 0.5) + rightPad; // ~one extra bar of breathing room
            plt.Axes.SetLimitsX(left, right);
        }

        private static void AddSubtitleBelowTitle(ScottPlot.Plot plt, string? subtitle)
        {
            if (string.IsNullOrWhiteSpace(subtitle))
            {
                return;
            }

            // create a little vertical headroom above current top
            var lim = plt.Axes.GetLimits();
            double spanY = lim.Top - lim.Bottom;
            double extra = Math.Max(spanY * 0.12, 1.0); // 12% of span or at least 1 unit

            plt.Axes.SetLimitsY(lim.Bottom, lim.Top + extra);

            // recompute after expanding
            lim = plt.Axes.GetLimits();
            double xMid = (lim.Left + lim.Right) / 2.0;
            double y = lim.Top - (extra * 0.35); // place subtitle inside the new padding

            var t = plt.Add.Text(subtitle, xMid, y);
            t.Alignment = ScottPlot.Alignment.UpperCenter;
            t.LabelFontSize = 14;
            t.Bold = false;
            t.Color = ScottPlot.Colors.Gray;
        }
    }
}