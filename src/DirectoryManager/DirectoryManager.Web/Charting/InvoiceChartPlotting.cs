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
                // Return an empty byte array if there is no data
                return Array.Empty<byte>();
            }

            // Filter for paid invoices in USD currency
            var paidInvoices = invoices
                .Where(i => i.PaymentStatus == Data.Enums.PaymentStatus.Paid && i.Currency == Data.Enums.Currency.USD)
                .ToList();

            // Group data by month and sum the income for each month
            var monthlyData = paidInvoices
                .GroupBy(i => new DateTime(i.CreateDate.Year, i.CreateDate.Month, 1)) // Group by year and month
                .Select(g => new
                {
                    Month = g.Key, // First day of the month for the X-axis
                    TotalIncome = (double)g.Sum(i => i.Amount) // Convert total income to double
                })
                .OrderBy(d => d.Month)
                .ToList();

            // Prepare a list of Bar objects for plotting
            var bars = monthlyData.Select((d, index) => new Bar
            {
                Position = index, // Sequential position for each month
                Value = d.TotalIncome, // Income for the month
                Label = d.Month.ToString("MMM yyyy") // Month label as string
            }).ToList();

            // Create the plot
            var myPlot = new Plot();

            // Add the bar plot with the list of Bar objects
            var barPlot = myPlot.Add.Bars(bars);

            // Configure the plot for the bar chart
            myPlot.Axes.Bottom.IsVisible = false;

            // Label the plot
            myPlot.Title($"Monthly {Data.Enums.Currency.USD.ToString()} Income");
            myPlot.XLabel("Month");
            myPlot.YLabel($"Total Income ({Data.Enums.Currency.USD.ToString()})");

            // Return the plot as a byte array to display in the view
            return myPlot.GetImageBytes(1200, 800, ImageFormat.Png);
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