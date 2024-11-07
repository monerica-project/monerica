using DirectoryManager.Data.Models.SponsoredListings;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class InvoicePlotting
    {
        public byte[] CreateMonthlyIncomeBarChart(List<SponsoredListingInvoice> invoices)
        {
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
    }
}