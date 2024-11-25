using System.Globalization;
using DirectoryManager.Data.Models;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class DirectoryEntryPlotting
    {
        public byte[] CreateWeeklyPlot(List<DirectoryEntry> entries)
        {
            // Ensure entries list is not empty
            if (!entries.Any())
            {
                throw new ArgumentException("Entries list is empty.");
            }

            entries = entries.Where(x => IncludedStatus(x)).ToList();

            // Group data by week and calculate cumulative total for each week
            var weeklyData = entries
                .GroupBy(entry => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                                entry.CreateDate,
                                CalendarWeekRule.FirstFourDayWeek,
                                DayOfWeek.Monday))
                .Select(group => new
                {
                    WeekStartDate = group.Min(entry => entry.CreateDate), // Take the earliest date in the week as the X-axis point
                    WeeklyTotal = group.Count() // Count of entries for the week
                })
                .OrderBy(d => d.WeekStartDate)
                .ToList();

            // Calculate cumulative totals
            double cumulativeTotal = 0;
            var plotDates = new List<DateTime>();
            var plotValues = new List<double>();

            foreach (var week in weeklyData)
            {
                cumulativeTotal += week.WeeklyTotal;
                plotDates.Add(week.WeekStartDate);
                plotValues.Add(cumulativeTotal); // Add cumulative total up to this week
            }

            // Create the plot
            var myPlot = new Plot();

            // Add a scatter plot with discrete points for cumulative totals
            var scatter = myPlot.Add.Scatter(plotDates.Select(d => d.ToOADate()).ToArray(), plotValues.ToArray());
            scatter.MarkerSize = 10;
            scatter.MarkerShape = MarkerShape.FilledSquare;
            scatter.LineStyle = LineStyle.None; // No connecting lines

            // Configure X-axis to display dates
            myPlot.Axes.DateTimeTicksBottom();

            // Label the plot
            myPlot.Title("Weekly Cumulative Totals");
            myPlot.XLabel("Week Starting");
            myPlot.YLabel("Total Accumulated Entries");

            // Return the plot as a byte array to display in the view
            return myPlot.GetImageBytes(1200, 800, ImageFormat.Png);
        }

        private static bool IncludedStatus(DirectoryEntry x)
        {
            return
                            x.DirectoryStatus == Data.Enums.DirectoryStatus.Admitted ||
                            x.DirectoryStatus == Data.Enums.DirectoryStatus.Verified ||
                            x.DirectoryStatus == Data.Enums.DirectoryStatus.Scam;
        }
    }
}