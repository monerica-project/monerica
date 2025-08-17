using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class InvoicePlotting
    {
        public byte[] CreateMonthlyIncomeBarChart(IEnumerable<SponsoredListingInvoice> invoices)
        {
            if (invoices == null || !invoices.Any())
            {
                return Array.Empty<byte>();
            }

            // Paid USD invoices only
            var paidInvoices = invoices
                .Where(i => i.PaymentStatus == Data.Enums.PaymentStatus.Paid && i.Currency == Data.Enums.Currency.USD)
                .ToList();

            // Sum by month (use first day of month as key)
            var monthlyData = paidInvoices
                .GroupBy(i => new DateTime(i.CreateDate.Year, i.CreateDate.Month, 1))
                .Select(g => new
                {
                    Month = g.Key,
                    TotalIncome = (double)g.Sum(i => i.Amount)
                })
                .OrderBy(d => d.Month)
                .ToList();

            // Build bars
            var bars = monthlyData.Select((d, index) => new Bar
            {
                Position = index,
                Value = d.TotalIncome
                // no need to set Label; we hide the bottom axis and draw our own labels
            }).ToList();

            var plt = new Plot();

            // bars
            var barPlot = plt.Add.Bars(bars);

            // hide bottom axis entirely (no month names there)
            plt.Axes.Bottom.IsVisible = false;

            // title & Y label
            plt.Title("Monthly USD Income");
            plt.YLabel("Total Income (USD)");

            // Fit with a bit of *top* headroom only (baseline at 0)
            plt.Axes.Margins(left: 0.06, right: 0.06, bottom: 0, top: 0.12);
            plt.Axes.AutoScale();

            // Pin the floor at exactly 0
            var lim = plt.Axes.GetLimits();
            plt.Axes.SetLimitsY(0, lim.Top);

            // Ensure enough room for text above tallest bar
            double maxBar = bars.Max(b => b.Value);
            double yOffset = Math.Max(maxBar * 0.04, 30); // 4% or $30 worth of headroom
            double neededTop = maxBar + yOffset;
            if (lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            // Add labels above each bar: amount (currency) and month on the next line
            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;

                string amount = $"{y:C0}";
                string month = monthlyData[i].Month.ToString("MMM yyyy");

                var txt = plt.Add.Text($"{amount}\n{month}", x, y + (yOffset * 0.5));
                txt.Alignment = ScottPlot.Alignment.LowerCenter; // anchor at bottom of text
                txt.LabelFontSize = 12;
            }

            // Render
            return plt.GetImageBytes(width: 1200, height: 800, format: ImageFormat.Png);
        }


        public byte[] CreateMonthlyAvgDailyRevenueChart(IEnumerable<SponsoredListingInvoice> invoices)
        {
            var paid = invoices
                .Where(i => i.Currency == Currency.USD && i.PaymentStatus == PaymentStatus.Paid)
                .ToList();
            if (!paid.Any())
            {
                return Array.Empty<byte>();
            }

            // Determine range of months
            var minStart = paid.Min(i => i.CampaignStartDate.Date);
            var maxEnd = paid.Max(i => i.CampaignEndDate.Date);

            // Build list of month start dates
            var months = new List<DateTime>();
            for (var m = new DateTime(minStart.Year, minStart.Month, 1);
                 m <= new DateTime(maxEnd.Year, maxEnd.Month, 1);
                 m = m.AddMonths(1))
            {
                months.Add(m);
            }

            // Compute average daily revenue per month
            var data = months.Select(m =>
            {
                int daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
                double total = 0;
                foreach (var inv in paid)
                {
                    var start = inv.CampaignStartDate.Date;
                    var end = inv.CampaignEndDate.Date;
                    double span = (end - start).TotalDays + 1;

                    var monthStart = m;
                    var monthEnd = m.AddMonths(1).AddDays(-1);

                    var overlapStart = start > monthStart ? start : monthStart;
                    var overlapEnd = end < monthEnd ? end : monthEnd;

                    if (overlapEnd >= overlapStart)
                    {
                        double overlapDays = (overlapEnd - overlapStart).TotalDays + 1;
                        total += ((double)inv.Amount / span) * overlapDays;
                    }
                }

                return new { Month = m, AvgDaily = total / daysInMonth };
            }).ToList();

            var now = DateTime.UtcNow;

            // Bars
            var bars = data.Select((d, idx) => new Bar
            {
                FillColor = (d.Month.Year == now.Year && d.Month.Month == now.Month)
                    ? Color.FromHex("#000000")
                    : Color.FromHex("#dddddd"),
                Position = idx,
                Value = d.AvgDaily,
                Label = d.Month.ToString("MMM yyyy")
            }).ToList();

            var plt = new Plot();
            var barPlot = plt.Add.Bars(bars);

            // Bottom month labels rotated (keep if you like)
            plt.Axes.Bottom.TickLabelStyle.Rotation = 90;

            // ---- Force non-negative Y and add headroom for labels ----
            plt.Axes.Margins(left: 0.06, right: 0.06, bottom: 0, top: 0.12); // no bottom margin
            plt.Axes.AutoScale();

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = maxBar * 0.05;                 // for value labels
            double neededTop = maxBar + Math.Max(yOffset, 1);

            var lim = plt.Axes.GetLimits();
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, Math.Max(lim.Top, neededTop));
            }
            // ----------------------------------------------------------

            // Value labels above each bar
            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                var txt = plt.Add.Text($"{y:C2}", x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            plt.Title("Average Daily Revenue");
            plt.XLabel("Month");
            plt.YLabel("USD per day");

            return plt.GetImageBytes(width: 1200, height: 600, format: ImageFormat.Png);
        }

        public byte[] CreateSubcategoryRevenuePieChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            IDictionary<int, string> categoryNames,
            IDictionary<int, string> subcategoryNames,
            IDictionary<int, int> subcategoryToCategory)
        {
            // 1) Build breakdown by SubCategoryId
            var breakdown = invoices
                .Where(i => i.PaymentStatus == PaymentStatus.Paid
                         && i.Currency == Currency.USD
                         && i.SubCategoryId.HasValue)
                .GroupBy(i => i.SubCategoryId!.Value)
                .Select(g =>
                {
                    int subId = g.Key;
                    subcategoryToCategory.TryGetValue(subId, out var catId);
                    categoryNames.TryGetValue(catId, out var catLabel);
                    subcategoryNames.TryGetValue(subId, out var subLabel);

                    string label = $"{catLabel ?? $"(Unknown Cat {catId})"} > {subLabel ?? $"(Unknown Sub {subId})"}";
                    double value = g.Sum(i => (double)i.Amount);
                    return (subId, label, value);
                })
                .Where(x => x.value > 0)
                .OrderByDescending(x => x.value)
                .ToArray();

            if (breakdown.Length == 0)
            {
                return Array.Empty<byte>();
            }

            // total revenue for percentage calculation
            double totalRevenue = breakdown.Sum(x => x.value);

            // 2) D3 Category20-ish palette in hex
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

            var palette = hexPalette.Select(ScottPlot.Color.FromHex).ToArray();

            // 3) Build PieSlice collection showing percent labels
            var slices = breakdown
                .Select((item, idx) =>
                {
                    double pct = totalRevenue > 0
                        ? Math.Round(item.value * 100.0 / totalRevenue, 1)
                        : 0;
                    var sliceColor = palette[idx % palette.Length];
                    return new PieSlice(item.value, sliceColor, $"{pct}%")
                    {
                        LegendText = item.label
                    };
                })
                .ToArray();

            // 4) Create and decorate the plot
            var plt = new Plot();

            // hide axes & grid entirely
            plt.HideAxesAndGrid();

            // add the pie
            var pie = plt.Add.Pie(slices);
            pie.DonutFraction = 0;            // no hole, full pie
            pie.SliceLabelDistance = 1.2;     // labels just outside slices

            // rotate so the largest slice starts at top (12 o'clock)
            pie.Rotation = Angle.FromDegrees(-90);

            // overlay legend at right edge
            plt.Title("Revenue by Subcategory");
            plt.ShowLegend(Edge.Right);

            // 5) Render to PNG
            return plt.GetImageBytes(800, 600, ImageFormat.Png);
        }
    }
}